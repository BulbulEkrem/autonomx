namespace AutoNomX.Domain;

public enum PipelineStatus
{
    Pending,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}
