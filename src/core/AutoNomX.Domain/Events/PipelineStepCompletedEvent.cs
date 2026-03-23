namespace AutoNomX.Domain.Events;

public sealed record PipelineStepCompletedEvent(
    Guid PipelineRunId,
    Guid ProjectId,
    string CompletedStep,
    string NextStep,
    bool Success
) : DomainEvent;
