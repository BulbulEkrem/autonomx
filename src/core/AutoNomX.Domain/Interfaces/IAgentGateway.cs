namespace AutoNomX.Domain.Interfaces;

/// <summary>
/// Gateway for communicating with the Python agent runtime via gRPC.
/// </summary>
public interface IAgentGateway
{
    /// <summary>Run an agent and return the result.</summary>
    Task<AgentExecutionResult> RunAgentAsync(
        AgentType agentType,
        AgentExecutionContext context,
        CancellationToken ct = default);

    /// <summary>Run an agent and stream progress events.</summary>
    IAsyncEnumerable<AgentProgressEvent> RunAgentStreamAsync(
        AgentType agentType,
        AgentExecutionContext context,
        CancellationToken ct = default);

    /// <summary>Get the status of a running agent execution.</summary>
    Task<AgentStatusInfo> GetAgentStatusAsync(string executionId, CancellationToken ct = default);

    /// <summary>Cancel a running agent execution.</summary>
    Task<bool> CancelAgentAsync(string executionId, string reason = "", CancellationToken ct = default);
}

/// <summary>Context passed to an agent for execution.</summary>
public record AgentExecutionContext(
    string ExecutionId,
    string ProjectId,
    string? Model = null,
    string? Provider = null,
    string? TaskId = null,
    string? TaskTitle = null,
    string? TaskDescription = null,
    string? ContextJson = null,
    Dictionary<string, string>? Metadata = null);

/// <summary>Result returned from an agent execution.</summary>
public record AgentExecutionResult(
    string ExecutionId,
    bool Success,
    string ResultJson,
    string? Error = null,
    int TotalTokens = 0,
    double DurationSeconds = 0,
    int Iterations = 0,
    string? ModelUsed = null);

/// <summary>Progress event from a streaming agent execution.</summary>
public record AgentProgressEvent(
    string ExecutionId,
    AgentProgressEventType EventType,
    string DataJson,
    DateTime Timestamp);

public enum AgentProgressEventType
{
    Log,
    Progress,
    Output,
    Error,
    Completed
}

/// <summary>Status of a running agent execution.</summary>
public record AgentStatusInfo(
    string ExecutionId,
    AgentType AgentType,
    string Status,
    double Progress);
