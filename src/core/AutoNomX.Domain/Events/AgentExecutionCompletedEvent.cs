namespace AutoNomX.Domain.Events;

public sealed record AgentExecutionCompletedEvent(
    string ExecutionId,
    Guid AgentId,
    AgentType AgentType,
    Guid ProjectId,
    Guid? TaskId,
    bool Success,
    string? Error
) : DomainEvent;
