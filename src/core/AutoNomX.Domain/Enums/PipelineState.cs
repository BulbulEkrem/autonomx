namespace AutoNomX.Domain;

/// <summary>
/// States for the pipeline state machine.
/// Maps to the pipeline execution lifecycle.
/// </summary>
public enum PipelineState
{
    Idle,
    Planning,
    Architecting,
    Coding,
    Testing,
    Reviewing,
    Completed,
    Failed,
    Paused
}
