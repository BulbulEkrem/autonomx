using System.Text.Json;
using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoNomX.Application.Services;

/// <summary>
/// Bridges the .NET orchestrator with the Python Model Manager agent.
/// Handles task assignment requests and model escalation on failures.
/// </summary>
public class ModelManagerService(
    IAgentGateway agentGateway,
    ICoderWorkerRepository workerRepo,
    IUnitOfWork unitOfWork,
    MetricsService metricsService,
    ILogger<ModelManagerService> logger)
{
    private const int EscalationThreshold = 2;

    /// <summary>
    /// Request Model Manager agent for optimal worker+model assignment.
    /// Returns the recommended worker ID and updates the worker's model if needed.
    /// </summary>
    public async Task<TaskAssignmentDecision> RequestTaskAssignmentAsync(
        TaskItem task,
        IReadOnlyList<CoderWorker> availableWorkers,
        CancellationToken ct = default)
    {
        // Build context for Model Manager agent
        var workers = availableWorkers.Select(w => new
        {
            id = w.Id.ToString(),
            name = w.Name,
            model = w.Model,
            provider = w.Provider,
            status = w.Status.ToString(),
        }).ToList();

        // Gather performance history
        var perfHistory = new Dictionary<string, object>();
        foreach (var worker in availableWorkers)
        {
            var perf = await metricsService.GetWorkerPerformanceAsync(worker.Id, ct);
            perfHistory[worker.Name] = new
            {
                total_tasks = perf.TotalTasks,
                success_rate = perf.SuccessRate,
                avg_iterations = perf.AvgIterations,
                avg_score = perf.AvgScore,
            };
        }

        var contextJson = JsonSerializer.Serialize(new
        {
            decision_type = "task_assignment",
            task = new
            {
                task_id = task.Id.ToString(),
                title = task.Title,
                description = task.Description,
                priority = task.Priority.ToString(),
                files_to_touch = task.FilesTouched,
            },
            workers,
            performance_history = perfHistory,
        });

        var result = await agentGateway.RunAgentAsync(
            AgentType.ModelManager,
            new AgentExecutionContext(
                ExecutionId: Guid.NewGuid().ToString(),
                ProjectId: task.ProjectId.ToString(),
                ContextJson: contextJson),
            ct);

        if (!result.Success)
        {
            logger.LogWarning("Model Manager failed, using default assignment");
            return CreateDefaultAssignment(availableWorkers);
        }

        return ParseAssignmentDecision(result.ResultJson, availableWorkers);
    }

    /// <summary>
    /// Handle a worker failure. If failure count >= threshold, request model escalation.
    /// </summary>
    public async Task<ModelSwitchDecision?> HandleFailureAsync(
        Guid workerId,
        Guid taskId,
        int failureCount,
        CancellationToken ct = default)
    {
        if (failureCount < EscalationThreshold)
        {
            logger.LogDebug("Failure count {Count} below threshold {Threshold}, no escalation",
                failureCount, EscalationThreshold);
            return null;
        }

        var worker = await workerRepo.GetByIdAsync(workerId, ct);
        if (worker is null) return null;

        var contextJson = JsonSerializer.Serialize(new
        {
            decision_type = "model_switch",
            failure_info = new
            {
                worker_id = workerId.ToString(),
                worker_name = worker.Name,
                current_model = worker.Model,
                task_id = taskId.ToString(),
                failure_count = failureCount,
            },
        });

        var result = await agentGateway.RunAgentAsync(
            AgentType.ModelManager,
            new AgentExecutionContext(
                ExecutionId: Guid.NewGuid().ToString(),
                ProjectId: Guid.Empty.ToString(),
                ContextJson: contextJson),
            ct);

        if (!result.Success)
        {
            logger.LogWarning("Model Manager escalation failed, applying default escalation");
            return ApplyDefaultEscalation(worker, failureCount);
        }

        var decision = ParseSwitchDecision(result.ResultJson, worker);

        // Apply the model switch
        if (decision is not null)
            await UpdateWorkerModelAsync(workerId, decision.NewModel, ct);

        return decision;
    }

    /// <summary>Update a worker's LLM model.</summary>
    public async Task UpdateWorkerModelAsync(
        Guid workerId,
        string newModel,
        CancellationToken ct = default)
    {
        var worker = await workerRepo.GetByIdAsync(workerId, ct);
        if (worker is null) return;

        var oldModel = worker.Model;
        worker.Model = newModel;
        await workerRepo.UpdateAsync(worker, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Worker {Name} model switched: {Old} → {New}",
            worker.Name, oldModel, newModel);
    }

    // ── Private helpers ─────────────────────────────────────────

    private TaskAssignmentDecision CreateDefaultAssignment(
        IReadOnlyList<CoderWorker> workers)
    {
        var worker = workers.FirstOrDefault();
        return new TaskAssignmentDecision(
            WorkerId: worker?.Id ?? Guid.Empty,
            WorkerName: worker?.Name ?? "unknown",
            Model: worker?.Model ?? "ollama/qwen2.5-coder:32b",
            Reasoning: "Default assignment (Model Manager unavailable)",
            FallbackModel: null,
            FallbackWorkerId: null);
    }

    private TaskAssignmentDecision ParseAssignmentDecision(
        string resultJson,
        IReadOnlyList<CoderWorker> workers)
    {
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;

            var assignedTo = root.TryGetProperty("assigned_to", out var at)
                ? at.GetString() : null;
            var model = root.TryGetProperty("model", out var m)
                ? m.GetString() : null;
            var reasoning = root.TryGetProperty("reasoning", out var r)
                ? r.GetString() : "Model Manager assignment";
            var fallbackModel = root.TryGetProperty("fallback_model", out var fm)
                ? fm.GetString() : null;
            var fallbackAgent = root.TryGetProperty("fallback_agent", out var fa)
                ? fa.GetString() : null;

            // Resolve worker by ID or name
            var worker = workers.FirstOrDefault(w =>
                w.Id.ToString() == assignedTo || w.Name == assignedTo);
            worker ??= workers.FirstOrDefault();

            var fallbackWorker = fallbackAgent is not null
                ? workers.FirstOrDefault(w => w.Id.ToString() == fallbackAgent || w.Name == fallbackAgent)
                : null;

            return new TaskAssignmentDecision(
                WorkerId: worker?.Id ?? Guid.Empty,
                WorkerName: worker?.Name ?? "unknown",
                Model: model ?? worker?.Model ?? "ollama/qwen2.5-coder:32b",
                Reasoning: reasoning ?? "",
                FallbackModel: fallbackModel,
                FallbackWorkerId: fallbackWorker?.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse assignment decision");
            return CreateDefaultAssignment(workers);
        }
    }

    private ModelSwitchDecision? ParseSwitchDecision(string resultJson, CoderWorker worker)
    {
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;

            var newModel = root.TryGetProperty("new_model", out var nm)
                ? nm.GetString() : null;

            if (newModel is null || newModel == worker.Model)
                return null;

            return new ModelSwitchDecision(
                WorkerId: worker.Id,
                WorkerName: worker.Name,
                OldModel: worker.Model,
                NewModel: newModel,
                Reason: root.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "",
                EscalationStep: root.TryGetProperty("escalation_step", out var es) ? es.GetInt32() : 1,
                Permanent: root.TryGetProperty("permanent", out var p) && p.GetBoolean());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse switch decision");
            return ApplyDefaultEscalation(worker, EscalationThreshold);
        }
    }

    private ModelSwitchDecision ApplyDefaultEscalation(CoderWorker worker, int failureCount)
    {
        // Default escalation: step up to best local model
        var escalationStep = failureCount switch
        {
            2 => 1,  // upgrade model one tier
            3 => 2,  // best local model
            >= 4 => 3, // would need API model
            _ => 1,
        };

        var newModel = escalationStep switch
        {
            1 => "ollama/qwen2.5-coder:32b",
            2 => "ollama/qwen2.5-coder:32b",
            _ => "openai/gpt-4o",
        };

        return new ModelSwitchDecision(
            WorkerId: worker.Id,
            WorkerName: worker.Name,
            OldModel: worker.Model,
            NewModel: newModel,
            Reason: $"Default escalation after {failureCount} failures",
            EscalationStep: escalationStep,
            Permanent: false);
    }
}

public record TaskAssignmentDecision(
    Guid WorkerId,
    string WorkerName,
    string Model,
    string Reasoning,
    string? FallbackModel,
    Guid? FallbackWorkerId);

public record ModelSwitchDecision(
    Guid WorkerId,
    string WorkerName,
    string OldModel,
    string NewModel,
    string Reason,
    int EscalationStep,
    bool Permanent);
