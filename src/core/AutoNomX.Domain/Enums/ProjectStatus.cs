namespace AutoNomX.Domain;

/// <summary>Lifecycle status of an AutoNomX project.</summary>
public enum ProjectStatus
{
    Created,
    Planning,
    InProgress,
    Testing,
    Reviewing,
    Completed,
    Failed,
    Paused
}
