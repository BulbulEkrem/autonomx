namespace AutoNomX.Domain.Events;

public sealed record TaskStatusChangedEvent(
    Guid TaskId,
    Guid ProjectId,
    TaskItemStatus OldStatus,
    TaskItemStatus NewStatus,
    string? AssignedWorker
) : DomainEvent;
