using System.Text.Json;
using AutoNomX.Domain;
using AutoNomX.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoNomX.Application.Services;

/// <summary>
/// Background service that listens for Postgres NOTIFY events
/// and routes them to the OrchestratorService for state transitions.
/// Channels: agent_events, pipeline_events
/// </summary>
public class PipelineEventHandler(
    IEventBus eventBus,
    OrchestratorService orchestrator,
    ILogger<PipelineEventHandler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PipelineEventHandler starting — subscribing to channels");

        await eventBus.SubscribeAsync("agent_events", HandleAgentEventAsync, stoppingToken);
        await eventBus.SubscribeAsync("task_events", HandleTaskEventAsync, stoppingToken);

        logger.LogInformation("PipelineEventHandler subscribed to agent_events, task_events");

        // Keep alive until shutdown
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleAgentEventAsync(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var eventType = root.GetProperty("type").GetString();
            logger.LogDebug("Received agent event: {Type}", eventType);

            if (eventType is "agent.completed" or "agent.failed")
            {
                var executionId = root.GetProperty("execution_id").GetString()!;
                var agentTypeName = root.GetProperty("agent_type").GetString()!;
                var success = root.TryGetProperty("success", out var s) && s.GetBoolean();
                var projectId = root.GetProperty("project_id").GetString()!;
                var error = root.TryGetProperty("error", out var e) ? e.GetString() : null;
                var resultJson = root.TryGetProperty("result", out var r) ? r.GetRawText() : "{}";

                if (!Enum.TryParse<AgentType>(ToPascalCase(agentTypeName), out var agentType))
                {
                    logger.LogWarning("Unknown agent type in event: {Type}", agentTypeName);
                    return;
                }

                // Find active pipeline for project
                var pipelineRunId = root.TryGetProperty("pipeline_run_id", out var prId)
                    ? Guid.Parse(prId.GetString()!)
                    : Guid.Empty;

                if (pipelineRunId == Guid.Empty)
                {
                    logger.LogDebug("No pipeline_run_id in event, skipping orchestration");
                    return;
                }

                var result = new AgentExecutionResult(
                    ExecutionId: executionId,
                    Success: success,
                    ResultJson: resultJson,
                    Error: error);

                await orchestrator.HandleAgentResultAsync(pipelineRunId, agentType, result);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling agent event: {Payload}", payload);
        }
    }

    private Task HandleTaskEventAsync(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var eventType = root.GetProperty("type").GetString();
            logger.LogDebug("Received task event: {Type}", eventType);

            // Task events are handled by TaskBoardService directly
            // This is for logging/monitoring
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling task event: {Payload}", payload);
        }

        return Task.CompletedTask;
    }

    private static string ToPascalCase(string snakeCase)
    {
        return string.Concat(snakeCase.Split('_')
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
    }
}
