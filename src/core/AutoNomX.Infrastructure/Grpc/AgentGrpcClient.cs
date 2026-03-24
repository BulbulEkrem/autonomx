using System.Runtime.CompilerServices;
using System.Text.Json;
using AutoNomX.Domain;
using AutoNomX.Domain.Interfaces;
using AutoNomX.Infrastructure.Grpc.Agent;
using AutoNomX.Infrastructure.Grpc.Common;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace AutoNomX.Infrastructure.Grpc;

/// <summary>
/// gRPC client that communicates with the Python agent runtime.
/// Implements <see cref="IAgentGateway"/> for the .NET orchestrator.
/// </summary>
public sealed class AgentGrpcClient : IAgentGateway, IDisposable
{
    private readonly AgentService.AgentServiceClient _client;
    private readonly GrpcChannel _channel;
    private readonly ILogger<AgentGrpcClient> _logger;

    public AgentGrpcClient(GrpcChannel channel, ILogger<AgentGrpcClient> logger)
    {
        _channel = channel;
        _client = new AgentService.AgentServiceClient(channel);
        _logger = logger;
    }

    public async Task<AgentExecutionResult> RunAgentAsync(
        Domain.AgentType agentType,
        AgentExecutionContext context,
        CancellationToken ct = default)
    {
        var request = BuildRequest(agentType, context);

        _logger.LogInformation(
            "RunAgent: execution={ExecutionId}, agent={AgentType}, project={ProjectId}",
            context.ExecutionId, agentType, context.ProjectId);

        try
        {
            var response = await _client.ExecuteAgentAsync(request, cancellationToken: ct);

            return new AgentExecutionResult(
                ExecutionId: response.ExecutionId,
                Success: response.Success,
                ResultJson: response.Result,
                Error: string.IsNullOrEmpty(response.Error) ? null : response.Error,
                TotalTokens: response.Metrics?.TotalTokens ?? 0,
                DurationSeconds: response.Metrics?.DurationSeconds ?? 0,
                Iterations: response.Metrics?.Iterations ?? 0,
                ModelUsed: response.Metrics?.ModelUsed);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "gRPC call failed: {Status}", ex.StatusCode);
            return new AgentExecutionResult(
                ExecutionId: context.ExecutionId,
                Success: false,
                ResultJson: "",
                Error: $"gRPC error: {ex.Status.Detail}");
        }
    }

    public async IAsyncEnumerable<AgentProgressEvent> RunAgentStreamAsync(
        Domain.AgentType agentType,
        AgentExecutionContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = BuildRequest(agentType, context);

        _logger.LogInformation(
            "RunAgentStream: execution={ExecutionId}, agent={AgentType}",
            context.ExecutionId, agentType);

        using var call = _client.ExecuteAgentStream(request, cancellationToken: ct);

        await foreach (var evt in call.ResponseStream.ReadAllAsync(ct))
        {
            yield return new AgentProgressEvent(
                ExecutionId: evt.ExecutionId,
                EventType: MapStreamEventType(evt.EventType),
                DataJson: evt.Data,
                Timestamp: DateTime.TryParse(evt.Timestamp, out var ts) ? ts : DateTime.UtcNow);
        }
    }

    public async Task<AgentStatusInfo> GetAgentStatusAsync(
        string executionId,
        CancellationToken ct = default)
    {
        var request = new GetAgentStatusRequest { ExecutionId = executionId };

        try
        {
            var response = await _client.GetAgentStatusAsync(request, cancellationToken: ct);

            return new AgentStatusInfo(
                ExecutionId: response.ExecutionId,
                AgentType: MapAgentType(response.AgentType),
                Status: response.Status,
                Progress: response.Progress);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "GetAgentStatus failed for {ExecutionId}", executionId);
            throw;
        }
    }

    public async Task<bool> CancelAgentAsync(
        string executionId,
        string reason = "",
        CancellationToken ct = default)
    {
        var request = new CancelAgentRequest
        {
            ExecutionId = executionId,
            Reason = reason,
        };

        try
        {
            var response = await _client.CancelAgentAsync(request, cancellationToken: ct);
            return response.Success;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "CancelAgent failed for {ExecutionId}", executionId);
            return false;
        }
    }

    private static ExecuteAgentRequest BuildRequest(
        Domain.AgentType agentType,
        AgentExecutionContext context)
    {
        var request = new ExecuteAgentRequest
        {
            ExecutionId = context.ExecutionId,
            ProjectId = context.ProjectId,
            AgentType = MapToProtoAgentType(agentType),
            Context = context.ContextJson ?? "",
        };

        // Config
        if (context.Model is not null)
        {
            request.Config = new Common.AgentConfig
            {
                AgentId = context.ExecutionId,
                AgentType = MapToProtoAgentType(agentType),
                Model = context.Model,
                Provider = context.Provider ?? "ollama",
            };
        }

        // Task
        if (context.TaskId is not null)
        {
            request.Task = new Common.TaskInfo
            {
                TaskId = context.TaskId,
                Title = context.TaskTitle ?? "",
                Description = context.TaskDescription ?? "",
            };
        }

        // Metadata
        if (context.Metadata is not null)
        {
            foreach (var (key, value) in context.Metadata)
            {
                request.Metadata[key] = value;
            }
        }

        return request;
    }

    private static Common.AgentType MapToProtoAgentType(Domain.AgentType agentType) => agentType switch
    {
        Domain.AgentType.ProductOwner => Common.AgentType.ProductOwner,
        Domain.AgentType.Planner => Common.AgentType.Planner,
        Domain.AgentType.Architect => Common.AgentType.Architect,
        Domain.AgentType.ModelManager => Common.AgentType.ModelManager,
        Domain.AgentType.Coder => Common.AgentType.Coder,
        Domain.AgentType.Tester => Common.AgentType.Tester,
        Domain.AgentType.Reviewer => Common.AgentType.Reviewer,
        _ => Common.AgentType.Unspecified,
    };

    private static Domain.AgentType MapAgentType(Common.AgentType protoType) => protoType switch
    {
        Common.AgentType.ProductOwner => Domain.AgentType.ProductOwner,
        Common.AgentType.Planner => Domain.AgentType.Planner,
        Common.AgentType.Architect => Domain.AgentType.Architect,
        Common.AgentType.ModelManager => Domain.AgentType.ModelManager,
        Common.AgentType.Coder => Domain.AgentType.Coder,
        Common.AgentType.Tester => Domain.AgentType.Tester,
        Common.AgentType.Reviewer => Domain.AgentType.Reviewer,
        _ => Domain.AgentType.ProductOwner,
    };

    private static AgentProgressEventType MapStreamEventType(StreamEventType eventType) => eventType switch
    {
        StreamEventType.Log => AgentProgressEventType.Log,
        StreamEventType.Progress => AgentProgressEventType.Progress,
        StreamEventType.Output => AgentProgressEventType.Output,
        StreamEventType.Error => AgentProgressEventType.Error,
        StreamEventType.Completed => AgentProgressEventType.Completed,
        _ => AgentProgressEventType.Log,
    };

    public void Dispose()
    {
        _channel.Dispose();
    }
}
