namespace AutoNomX.Domain.Events;

public sealed record ProjectStatusChangedEvent(
    Guid ProjectId,
    ProjectStatus OldStatus,
    ProjectStatus NewStatus
) : DomainEvent;
