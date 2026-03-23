namespace AutoNomX.Domain.Events;

public sealed record WorkerStatusChangedEvent(
    Guid WorkerId,
    WorkerStatus OldStatus,
    WorkerStatus NewStatus,
    Guid? TaskId
) : DomainEvent;
