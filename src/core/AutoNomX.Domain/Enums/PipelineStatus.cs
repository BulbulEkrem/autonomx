namespace AutoNomX.Domain;

/// <summary>Overall status of a pipeline execution run.</summary>
public enum PipelineStatus
{
    Pending,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}
