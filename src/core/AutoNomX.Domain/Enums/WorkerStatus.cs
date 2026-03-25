namespace AutoNomX.Domain;

/// <summary>Current operational status of a coder worker.</summary>
public enum WorkerStatus
{
    Idle,
    Working,
    Testing,
    Reviewing,
    Offline
}
