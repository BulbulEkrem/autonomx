namespace AutoNomX.Domain;

/// <summary>
/// Triggers that cause state transitions in the pipeline state machine.
/// </summary>
public enum PipelineTrigger
{
    Start,
    PlanReady,
    ArchitectureReady,
    CodeReady,
    TestPassed,
    TestFailed,
    ReviewApproved,
    ReviewRejected,
    AllTasksCompleted,
    Pause,
    Resume,
    Error
}
