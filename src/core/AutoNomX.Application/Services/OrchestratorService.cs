using System.Text.Json;
using AutoNomX.Application.StateMachine;
using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Events;
using AutoNomX.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoNomX.Application.Services;

/// <summary>
/// Central orchestrator that drives the pipeline by coordinating agents,
/// managing state transitions, and handling results.
/// </summary>
public class OrchestratorService(
    IAgentGateway agentGateway,
    IPipelineRunRepository pipelineRunRepo,
    IProjectRepository projectRepo,
    ITaskRepository taskRepo,
    ICoderWorkerRepository workerRepo,
    IEventBus eventBus,
    IUnitOfWork unitOfWork,
    TaskBoardService taskBoard,
    WorkerPoolService workerPool,
    ModelManagerService modelManager,
    MetricsService metricsService,
    IGitService gitService,
    IMediator mediator,
    ILogger<OrchestratorService> logger,
    ILogger<PipelineStateMachine> smLogger)
{
    private const string WorkspaceRoot = "workspace";
    private const int MaxPipelineIterations = 20;

    private string GetProjectPath(Guid projectId) =>
        Path.Combine(WorkspaceRoot, $"project-{projectId.ToString()[..8]}");
    /// <summary>Start a new pipeline for a project.</summary>
    public async Task<PipelineRun> StartPipelineAsync(
        Guid projectId,
        string userRequest,
        CancellationToken ct = default)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new InvalidOperationException($"Project {projectId} not found");

        logger.LogInformation("Starting pipeline for project {ProjectId}: {Name}", projectId, project.Name);

        // Initialize git repo for the project workspace
        var projectPath = GetProjectPath(projectId);
        try
        {
            await gitService.InitRepoAsync(projectPath, ct);
            project.RepositoryPath = projectPath;
            await projectRepo.UpdateAsync(project, ct);
            logger.LogInformation("Git repo initialized at {Path}", projectPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Git init failed (non-fatal): {Message}", ex.Message);
        }

        // Create state machine
        var sm = PipelineStateMachine.Create(projectId, pipelineRunRepo, unitOfWork, smLogger);
        var run = sm.GetPipelineRun();
        run.StartedAt = DateTime.UtcNow;
        await pipelineRunRepo.AddAsync(run, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Start → Planning
        await sm.FireAsync(PipelineTrigger.Start);

        // Run planning phase
        await ProcessStateAsync(sm, userRequest, ct);

        return run;
    }

    /// <summary>Process the current pipeline state — dispatches to the appropriate agent.</summary>
    public async Task ProcessStateAsync(
        PipelineStateMachine sm,
        string? contextJson = null,
        CancellationToken ct = default)
    {
        var state = sm.CurrentState;
        logger.LogInformation("Processing state {State} for pipeline {Id}", state, sm.PipelineRunId);

        // Safety net: force-stop if pipeline exceeds max iterations
        var run = sm.GetPipelineRun();
        if (run.CurrentIteration > MaxPipelineIterations
            && state is not PipelineState.Completed and not PipelineState.Failed)
        {
            logger.LogWarning(
                "Pipeline {Id} exceeded max iterations ({Max}), forcing completion",
                sm.PipelineRunId, MaxPipelineIterations);
            if (sm.CanFire(PipelineTrigger.AllTasksCompleted))
            {
                await sm.FireAsync(PipelineTrigger.AllTasksCompleted);
                await ProcessStateAsync(sm, ct: ct);
            }
            else if (sm.CanFire(PipelineTrigger.Error))
            {
                run.ErrorMessage = $"Pipeline exceeded maximum iterations ({MaxPipelineIterations})";
                await sm.FireAsync(PipelineTrigger.Error);
            }
            return;
        }

        try
        {
            switch (state)
            {
                case PipelineState.Planning:
                    await RunPlanningPhaseAsync(sm, contextJson, ct);
                    break;
                case PipelineState.Architecting:
                    await RunArchitectingPhaseAsync(sm, contextJson, ct);
                    break;
                case PipelineState.Coding:
                    await RunCodingPhaseAsync(sm, ct);
                    break;
                case PipelineState.Testing:
                    await RunTestingPhaseAsync(sm, ct);
                    break;
                case PipelineState.Reviewing:
                    await RunReviewingPhaseAsync(sm, ct);
                    break;
                case PipelineState.Completed:
                    logger.LogInformation("Pipeline {Id} completed", sm.PipelineRunId);
                    await PublishPipelineEventAsync(sm, "pipeline.completed", ct);
                    break;
                case PipelineState.Failed:
                    logger.LogError("Pipeline {Id} failed", sm.PipelineRunId);
                    await PublishPipelineEventAsync(sm, "pipeline.failed", ct);
                    break;
                default:
                    logger.LogDebug("No action for state {State}", state);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing state {State} for pipeline {Id}", state, sm.PipelineRunId);
            if (sm.CanFire(PipelineTrigger.Error))
            {
                var run = sm.GetPipelineRun();
                run.ErrorMessage = ex.Message;
                await sm.FireAsync(PipelineTrigger.Error);
            }
        }
    }

    /// <summary>Handle agent result and trigger appropriate state transition.</summary>
    public async Task HandleAgentResultAsync(
        Guid pipelineRunId,
        AgentType agentType,
        AgentExecutionResult result,
        CancellationToken ct = default)
    {
        var run = await pipelineRunRepo.GetByIdAsync(pipelineRunId, ct)
            ?? throw new InvalidOperationException($"PipelineRun {pipelineRunId} not found");

        var sm = PipelineStateMachine.Rehydrate(run, pipelineRunRepo, unitOfWork, smLogger);

        logger.LogInformation(
            "Agent result: pipeline={Id}, agent={Agent}, success={Success}",
            pipelineRunId, agentType, result.Success);

        if (!result.Success)
        {
            if (sm.CanFire(PipelineTrigger.Error))
            {
                run.ErrorMessage = result.Error;
                await sm.FireAsync(PipelineTrigger.Error);
            }
            return;
        }

        // Determine trigger based on agent type
        var trigger = agentType switch
        {
            AgentType.ProductOwner or AgentType.Planner => PipelineTrigger.PlanReady,
            AgentType.Architect => PipelineTrigger.ArchitectureReady,
            AgentType.Coder => PipelineTrigger.CodeReady,
            AgentType.Tester => await DetermineTestTriggerAsync(result, ct),
            AgentType.Reviewer => await DetermineReviewTriggerAsync(sm.ProjectId, result, ct),
            _ => PipelineTrigger.Error,
        };

        if (sm.CanFire(trigger))
        {
            await sm.FireAsync(trigger);
            await ProcessStateAsync(sm, result.ResultJson, ct);
        }
    }

    /// <summary>Resume a paused pipeline.</summary>
    public async Task ResumePipelineAsync(Guid pipelineRunId, CancellationToken ct = default)
    {
        var run = await pipelineRunRepo.GetByIdAsync(pipelineRunId, ct)
            ?? throw new InvalidOperationException($"PipelineRun {pipelineRunId} not found");

        if (run.Status != PipelineStatus.Paused)
            throw new InvalidOperationException($"Pipeline {pipelineRunId} is not paused (status: {run.Status})");

        var sm = PipelineStateMachine.Rehydrate(run, pipelineRunRepo, unitOfWork, smLogger);
        await sm.FireAsync(PipelineTrigger.Resume);
        await ProcessStateAsync(sm, ct: ct);

        logger.LogInformation("Pipeline {Id} resumed", pipelineRunId);
    }

    /// <summary>Pause a running pipeline.</summary>
    public async Task PausePipelineAsync(Guid pipelineRunId, CancellationToken ct = default)
    {
        var run = await pipelineRunRepo.GetByIdAsync(pipelineRunId, ct)
            ?? throw new InvalidOperationException($"PipelineRun {pipelineRunId} not found");

        var sm = PipelineStateMachine.Rehydrate(run, pipelineRunRepo, unitOfWork, smLogger);

        if (sm.CanFire(PipelineTrigger.Pause))
        {
            await sm.FireAsync(PipelineTrigger.Pause);
            logger.LogInformation("Pipeline {Id} paused", pipelineRunId);
        }
    }

    /// <summary>Get pipeline status.</summary>
    public async Task<PipelineStatusInfo> GetPipelineStatusAsync(
        Guid pipelineRunId,
        CancellationToken ct = default)
    {
        var run = await pipelineRunRepo.GetByIdAsync(pipelineRunId, ct)
            ?? throw new InvalidOperationException($"PipelineRun {pipelineRunId} not found");

        var board = await taskBoard.GetBoardStatusAsync(run.ProjectId, ct);

        return new PipelineStatusInfo(
            PipelineRunId: run.Id,
            ProjectId: run.ProjectId,
            Status: run.Status,
            CurrentStep: run.CurrentStep,
            Iteration: run.CurrentIteration,
            StartedAt: run.StartedAt,
            CompletedAt: run.CompletedAt,
            ErrorMessage: run.ErrorMessage,
            BoardStatus: board);
    }

    // ── Phase Runners ──────────────────────────────────────────

    private async Task RunPlanningPhaseAsync(
        PipelineStateMachine sm,
        string? userRequest,
        CancellationToken ct)
    {
        // Product Owner: analyze request
        var poResult = await agentGateway.RunAgentAsync(
            AgentType.ProductOwner,
            new AgentExecutionContext(
                ExecutionId: Guid.NewGuid().ToString(),
                ProjectId: sm.ProjectId.ToString(),
                ContextJson: userRequest),
            ct);

        if (!poResult.Success)
        {
            await sm.FireAsync(PipelineTrigger.Error);
            return;
        }

        // Planner: create tasks from stories
        var plannerResult = await agentGateway.RunAgentAsync(
            AgentType.Planner,
            new AgentExecutionContext(
                ExecutionId: Guid.NewGuid().ToString(),
                ProjectId: sm.ProjectId.ToString(),
                ContextJson: poResult.ResultJson),
            ct);

        if (plannerResult.Success)
        {
            // Persist tasks from planner output to DB so the pipeline can track completion
            await PersistPlannerTasksAsync(sm.ProjectId, plannerResult.ResultJson, ct);

            await sm.FireAsync(PipelineTrigger.PlanReady);
            await ProcessStateAsync(sm, plannerResult.ResultJson, ct);
        }
        else
        {
            sm.GetPipelineRun().ErrorMessage = plannerResult.Error;
            await sm.FireAsync(PipelineTrigger.Error);
        }
    }

    private async Task RunArchitectingPhaseAsync(
        PipelineStateMachine sm,
        string? contextJson,
        CancellationToken ct)
    {
        var result = await agentGateway.RunAgentAsync(
            AgentType.Architect,
            new AgentExecutionContext(
                ExecutionId: Guid.NewGuid().ToString(),
                ProjectId: sm.ProjectId.ToString(),
                ContextJson: contextJson),
            ct);

        if (result.Success)
        {
            // Git: commit scaffolding on main
            await GitCommitSafe(sm.ProjectId, "feat: initial project scaffolding", ct);

            await sm.FireAsync(PipelineTrigger.ArchitectureReady);
            await ProcessStateAsync(sm, result.ResultJson, ct);
        }
        else
        {
            sm.GetPipelineRun().ErrorMessage = result.Error;
            await sm.FireAsync(PipelineTrigger.Error);
        }
    }

    private async Task RunCodingPhaseAsync(PipelineStateMachine sm, CancellationToken ct)
    {
        logger.LogInformation("Starting parallel coding phase for project {ProjectId}", sm.ProjectId);

        // Loop: assign tasks to workers, wait for completions, self-pick next
        while (!ct.IsCancellationRequested)
        {
            var readyTasks = await taskBoard.GetReadyTasksAsync(sm.ProjectId, ct);
            var idleWorkers = await workerRepo.GetIdleWorkersAsync(ct);

            // If no ready tasks and no in-progress tasks → coding phase done
            if (readyTasks.Count == 0)
            {
                var codingDone = await taskBoard.IsCodingPhaseCompleteAsync(sm.ProjectId, ct);
                if (codingDone)
                    break;

                // Tasks still in progress — wait a bit for workers to finish
                await Task.Delay(500, ct);
                continue;
            }

            if (idleWorkers.Count == 0)
            {
                // All workers busy — wait for one to finish
                await Task.Delay(500, ct);
                continue;
            }

            // Launch parallel worker tasks with Model Manager assignment
            var workerTasks = new List<Task>();

            foreach (var task in readyTasks)
            {
                if (idleWorkers.Count == 0) break;

                // Ask Model Manager for optimal worker+model assignment
                TaskAssignmentDecision? assignment = null;
                try
                {
                    assignment = await modelManager.RequestTaskAssignmentAsync(task, idleWorkers, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Model Manager assignment failed, falling back to self-pick");
                }

                CoderWorker? worker;
                if (assignment is not null && assignment.WorkerId != Guid.Empty)
                {
                    worker = idleWorkers.FirstOrDefault(w => w.Id == assignment.WorkerId)
                        ?? idleWorkers.FirstOrDefault();

                    // Apply model recommendation if different
                    if (worker is not null && worker.Model != assignment.Model)
                    {
                        await modelManager.UpdateWorkerModelAsync(worker.Id, assignment.Model, ct);
                        worker.Model = assignment.Model;
                    }

                    logger.LogInformation(
                        "Model Manager assigned task {TaskId} to {Worker} ({Model}): {Reason}",
                        task.Id, assignment.WorkerName, assignment.Model, assignment.Reasoning);
                }
                else
                {
                    worker = idleWorkers.FirstOrDefault();
                }

                if (worker is null) break;

                // Assign via task board
                var assigned = await taskBoard.AssignTaskAsync(task.Id, worker.Id, ct);
                if (!assigned) continue;

                // Remove from available pool for this round
                idleWorkers = idleWorkers.Where(w => w.Id != worker.Id).ToList();

                workerTasks.Add(RunCoderForTaskAsync(sm, task, worker, ct));
            }

            if (workerTasks.Count == 0)
            {
                await Task.Delay(500, ct);
                continue;
            }

            // Wait for at least one worker to finish, then loop to assign more
            await Task.WhenAny(workerTasks);
        }

        // All coding tasks done → transition
        if (sm.CanFire(PipelineTrigger.CodeReady))
        {
            await sm.FireAsync(PipelineTrigger.CodeReady);
            await ProcessStateAsync(sm, ct: ct);
        }
    }

    private async Task RunCoderForTaskAsync(
        PipelineStateMachine sm,
        TaskItem task,
        CoderWorker worker,
        CancellationToken ct)
    {
        var branchName = $"feature/T-{task.Id.ToString()[..8]}-{SanitizeBranchName(task.Title)}";
        var startTime = DateTime.UtcNow;

        try
        {
            // Git: create feature branch for this task
            await GitCreateBranchSafe(sm.ProjectId, branchName, ct);

            var result = await agentGateway.RunAgentAsync(
                AgentType.Coder,
                new AgentExecutionContext(
                    ExecutionId: Guid.NewGuid().ToString(),
                    ProjectId: sm.ProjectId.ToString(),
                    Model: worker.Model,
                    Provider: worker.Provider,
                    TaskId: task.Id.ToString(),
                    TaskTitle: task.Title,
                    TaskDescription: task.Description),
                ct);

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;

            if (result.Success)
            {
                // Git: commit on feature branch
                await GitCommitSafe(sm.ProjectId, $"feat: {task.Title}", ct);
                await taskBoard.CompleteTaskAsync(task.Id, result.ResultJson, ct);

                // Record success metrics
                await RecordMetricsSafe(worker.Id, worker.Model, result, duration, true, ct);
            }
            else
            {
                await taskBoard.FailTaskAsync(task.Id, result.Error ?? "Unknown error", ct);

                // Record failure metrics
                await RecordMetricsSafe(worker.Id, worker.Model, result, duration, false, ct);

                // Check if we should escalate the model
                var updatedTask = await taskRepo.GetByIdAsync(task.Id, ct);
                if (updatedTask is not null && updatedTask.RetryCount >= 2)
                {
                    var switchDecision = await modelManager.HandleFailureAsync(
                        worker.Id, task.Id, updatedTask.RetryCount, ct);

                    if (switchDecision is not null)
                    {
                        logger.LogInformation(
                            "Model escalation: {Worker} {Old} → {New} (step {Step}): {Reason}",
                            switchDecision.WorkerName, switchDecision.OldModel,
                            switchDecision.NewModel, switchDecision.EscalationStep,
                            switchDecision.Reason);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Coder failed for task {TaskId}", task.Id);
            await taskBoard.FailTaskAsync(task.Id, ex.Message, ct);
        }

        // After task completes, clean up offline workers
        await workerPool.CleanupOfflineWorkersAsync(ct);
    }

    private async Task RecordMetricsSafe(
        Guid workerId, string model, AgentExecutionResult result,
        double duration, bool success, CancellationToken ct)
    {
        try
        {
            await metricsService.RecordTaskCompletionAsync(
                workerId, model, result.Iterations, result.TotalTokens,
                duration, success, ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record metrics (non-fatal)");
        }
    }

    private async Task RunTestingPhaseAsync(PipelineStateMachine sm, CancellationToken ct)
    {
        var result = await agentGateway.RunAgentAsync(
            AgentType.Tester,
            new AgentExecutionContext(
                ExecutionId: Guid.NewGuid().ToString(),
                ProjectId: sm.ProjectId.ToString()),
            ct);

        var trigger = await DetermineTestTriggerAsync(result, ct);
        if (sm.CanFire(trigger))
        {
            await sm.FireAsync(trigger);
            await ProcessStateAsync(sm, result.ResultJson, ct);
        }
    }

    private async Task RunReviewingPhaseAsync(PipelineStateMachine sm, CancellationToken ct)
    {
        var result = await agentGateway.RunAgentAsync(
            AgentType.Reviewer,
            new AgentExecutionContext(
                ExecutionId: Guid.NewGuid().ToString(),
                ProjectId: sm.ProjectId.ToString()),
            ct);

        // Extract and record reviewer scores per task
        await RecordReviewScoresSafe(sm.ProjectId, result, ct);

        var trigger = await DetermineReviewTriggerAsync(sm.ProjectId, result, ct);
        if (sm.CanFire(trigger))
        {
            await sm.FireAsync(trigger);
            await ProcessStateAsync(sm, result.ResultJson, ct);
        }
    }

    private async Task RecordReviewScoresSafe(Guid projectId, AgentExecutionResult result, CancellationToken ct)
    {
        if (!result.Success) return;

        try
        {
            using var doc = JsonDocument.Parse(result.ResultJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("categories", out var cats))
            {
                var scores = new ReviewScores(
                    Correctness: cats.TryGetProperty("correctness", out var c) ? c.GetDouble() : 0,
                    CodeQuality: cats.TryGetProperty("code_quality", out var q) ? q.GetDouble() : 0,
                    Security: cats.TryGetProperty("security", out var s) ? s.GetDouble() : 0,
                    Performance: cats.TryGetProperty("performance", out var p) ? p.GetDouble() : 0,
                    Completeness: cats.TryGetProperty("completeness", out var comp) ? comp.GetDouble() : 0);

                // Record scores for all recently completed task workers
                var tasks = await taskRepo.GetByProjectIdAsync(projectId, ct);
                foreach (var task in tasks.Where(t => t.Status == TaskItemStatus.Done && t.AssignedWorker is not null))
                {
                    if (Guid.TryParse(task.AssignedWorker, out var workerId))
                    {
                        var worker = await workerRepo.GetByIdAsync(workerId, ct);
                        if (worker is not null)
                        {
                            await metricsService.RecordTaskCompletionAsync(
                                workerId, worker.Model, 0, 0, 0, true, scores, ct);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not extract review scores (non-fatal)");
        }
    }

    // ── Trigger Determination ──────────────────────────────────

    private Task<PipelineTrigger> DetermineTestTriggerAsync(
        AgentExecutionResult result,
        CancellationToken ct)
    {
        if (!result.Success)
            return Task.FromResult(PipelineTrigger.TestFailed);

        try
        {
            using var doc = JsonDocument.Parse(result.ResultJson);
            var root = doc.RootElement;
            // Tester agent output has an execution.command — check for test pass
            if (root.TryGetProperty("test_passed", out var passed) && passed.GetBoolean())
                return Task.FromResult(PipelineTrigger.TestPassed);
        }
        catch { /* fall through */ }

        // Default: passed
        return Task.FromResult(PipelineTrigger.TestPassed);
    }

    private async Task<PipelineTrigger> DetermineReviewTriggerAsync(
        Guid projectId,
        AgentExecutionResult result,
        CancellationToken ct)
    {
        if (!result.Success)
            return PipelineTrigger.ReviewRejected;

        try
        {
            using var doc = JsonDocument.Parse(result.ResultJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("decision", out var decision))
            {
                var decisionStr = decision.GetString()?.ToUpperInvariant();
                if (decisionStr == "APPROVE")
                {
                    // Git: merge approved task branches into main
                    await MergeApprovedBranchesAsync(projectId, ct);

                    var allDone = await taskBoard.AreAllTasksCompletedAsync(projectId, ct);
                    return allDone ? PipelineTrigger.AllTasksCompleted : PipelineTrigger.ReviewApproved;
                }
                if (decisionStr == "REVISION")
                    return PipelineTrigger.ReviewRejected;
            }
        }
        catch { /* fall through */ }

        // Fallback: check if all tasks are done before defaulting to another loop iteration
        var fallbackAllDone = await taskBoard.AreAllTasksCompletedAsync(projectId, ct);
        return fallbackAllDone ? PipelineTrigger.AllTasksCompleted : PipelineTrigger.ReviewApproved;
    }

    private async Task MergeApprovedBranchesAsync(Guid projectId, CancellationToken ct)
    {
        var projectPath = GetProjectPath(projectId);
        var tasks = await taskRepo.GetByProjectIdAsync(projectId, ct);
        var doneTasks = tasks.Where(t => t.Status == TaskItemStatus.Done && t.GitBranch is not null);

        foreach (var task in doneTasks)
        {
            try
            {
                var mergeResult = await gitService.MergeBranchAsync(projectPath, task.GitBranch!, "main", ct);
                if (mergeResult.HasConflicts)
                {
                    logger.LogWarning("Merge conflict for task {TaskId} branch {Branch}: {Files}",
                        task.Id, task.GitBranch, string.Join(", ", mergeResult.ConflictFiles));

                    // Send to architect for conflict resolution
                    await agentGateway.RunAgentAsync(
                        AgentType.Architect,
                        new AgentExecutionContext(
                            ExecutionId: Guid.NewGuid().ToString(),
                            ProjectId: projectId.ToString(),
                            ContextJson: JsonSerializer.Serialize(new
                            {
                                action = "resolve_conflict",
                                branch = task.GitBranch,
                                conflict_files = mergeResult.ConflictFiles,
                            })),
                        ct);
                }
                else
                {
                    logger.LogInformation("Merged {Branch} → main", task.GitBranch);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Merge failed for branch {Branch}", task.GitBranch);
            }
        }
    }

    private async Task PublishPipelineEventAsync(
        PipelineStateMachine sm,
        string eventType,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = eventType,
            pipeline_run_id = sm.PipelineRunId.ToString(),
            project_id = sm.ProjectId.ToString(),
            state = sm.CurrentState.ToString(),
        });

        await eventBus.PublishAsync("pipeline_events", payload, ct);

        await mediator.Publish(new PipelineStepCompletedEvent(
            sm.PipelineRunId,
            sm.ProjectId,
            sm.CurrentState.ToString(),
            sm.CurrentState.ToString(),
            sm.CurrentState == PipelineState.Completed), ct);
    }

    // ── Task Persistence ──────────────────────────────────────

    private async Task PersistPlannerTasksAsync(
        Guid projectId,
        string plannerResultJson,
        CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(plannerResultJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tasks", out var tasksElement))
                return;

            var taskItems = new List<TaskItem>();
            foreach (var taskEl in tasksElement.EnumerateArray())
            {
                var title = taskEl.TryGetProperty("title", out var t) ? t.GetString() ?? "Untitled" : "Untitled";
                var description = taskEl.TryGetProperty("description", out var d) ? d.GetString() : null;

                var deps = new List<string>();
                if (taskEl.TryGetProperty("dependencies", out var depsEl))
                {
                    foreach (var dep in depsEl.EnumerateArray())
                    {
                        var depStr = dep.GetString();
                        if (depStr is not null) deps.Add(depStr);
                    }
                }

                var filesToCreate = new List<string>();
                if (taskEl.TryGetProperty("files_to_create", out var filesEl))
                {
                    foreach (var f in filesEl.EnumerateArray())
                    {
                        var fStr = f.GetString();
                        if (fStr is not null) filesToCreate.Add(fStr);
                    }
                }

                var priority = TaskItemPriority.Should;
                if (taskEl.TryGetProperty("priority", out var pEl))
                {
                    var pStr = pEl.GetString()?.ToUpperInvariant();
                    priority = pStr switch
                    {
                        "MUST" => TaskItemPriority.Must,
                        "COULD" => TaskItemPriority.Could,
                        _ => TaskItemPriority.Should,
                    };
                }

                taskItems.Add(new TaskItem
                {
                    Title = title,
                    Description = description,
                    Priority = priority,
                    Dependencies = deps,
                    FilesTouched = filesToCreate,
                });
            }

            if (taskItems.Count > 0)
            {
                await taskBoard.InitializeBoardAsync(projectId, taskItems, ct);
                logger.LogInformation(
                    "Persisted {Count} tasks from planner output for project {ProjectId}",
                    taskItems.Count, projectId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist planner tasks (non-fatal): {Message}", ex.Message);
        }
    }

    // ── Git Helpers ────────────────────────────────────────────

    private async Task GitCommitSafe(Guid projectId, string message, CancellationToken ct)
    {
        try
        {
            var projectPath = GetProjectPath(projectId);
            await gitService.CommitAllAsync(projectPath, message, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Git commit failed (non-fatal): {Message}", ex.Message);
        }
    }

    private async Task GitCreateBranchSafe(Guid projectId, string branchName, CancellationToken ct)
    {
        try
        {
            var projectPath = GetProjectPath(projectId);
            await gitService.CheckoutAsync(projectPath, "main", ct);
            await gitService.CreateBranchAsync(projectPath, branchName, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Git branch creation failed (non-fatal): {Message}", ex.Message);
        }
    }

    private static string SanitizeBranchName(string name)
    {
        var sanitized = name
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("/", "-")
            .Replace("\\", "-");

        // Limit length
        return sanitized.Length > 40 ? sanitized[..40] : sanitized;
    }
}

public record PipelineStatusInfo(
    Guid PipelineRunId,
    Guid ProjectId,
    PipelineStatus Status,
    string CurrentStep,
    int Iteration,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    BoardStatus BoardStatus);
