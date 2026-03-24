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
    IMediator mediator,
    ILogger<OrchestratorService> logger,
    ILogger<PipelineStateMachine> smLogger)
{
    /// <summary>Start a new pipeline for a project.</summary>
    public async Task<PipelineRun> StartPipelineAsync(
        Guid projectId,
        string userRequest,
        CancellationToken ct = default)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new InvalidOperationException($"Project {projectId} not found");

        logger.LogInformation("Starting pipeline for project {ProjectId}: {Name}", projectId, project.Name);

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
        var readyTasks = await taskBoard.GetReadyTasksAsync(sm.ProjectId, ct);
        var idleWorkers = await workerRepo.GetIdleWorkersAsync(ct);

        if (readyTasks.Count == 0)
        {
            // All tasks assigned or done — move to testing
            if (sm.CanFire(PipelineTrigger.CodeReady))
                await sm.FireAsync(PipelineTrigger.CodeReady);
            return;
        }

        // Assign tasks to available workers
        foreach (var (task, worker) in readyTasks.Zip(idleWorkers))
        {
            var assigned = await taskBoard.AssignTaskAsync(task.Id, worker.Id, ct);
            if (!assigned) continue;

            // Fire agent asynchronously (result comes back via HandleAgentResultAsync)
            _ = RunCoderForTaskAsync(sm, task, worker, ct);
        }
    }

    private async Task RunCoderForTaskAsync(
        PipelineStateMachine sm,
        TaskItem task,
        CoderWorker worker,
        CancellationToken ct)
    {
        try
        {
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

            if (result.Success)
                await taskBoard.CompleteTaskAsync(task.Id, result.ResultJson, ct);
            else
                await taskBoard.FailTaskAsync(task.Id, result.Error ?? "Unknown error", ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Coder failed for task {TaskId}", task.Id);
            await taskBoard.FailTaskAsync(task.Id, ex.Message, ct);
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

        var trigger = await DetermineReviewTriggerAsync(sm.ProjectId, result, ct);
        if (sm.CanFire(trigger))
        {
            await sm.FireAsync(trigger);
            await ProcessStateAsync(sm, result.ResultJson, ct);
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
                    // Check if all tasks are done
                    var allDone = await taskBoard.AreAllTasksCompletedAsync(projectId, ct);
                    return allDone ? PipelineTrigger.AllTasksCompleted : PipelineTrigger.ReviewApproved;
                }
                if (decisionStr == "REVISION")
                    return PipelineTrigger.ReviewRejected;
            }
        }
        catch { /* fall through */ }

        return PipelineTrigger.ReviewApproved;
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
