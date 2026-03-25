namespace AutoNomX.Domain;

/// <summary>Status of a task item on the Kanban task board.</summary>
public enum TaskItemStatus
{
    Ready,
    InProgress,
    Testing,
    Review,
    Done,
    Failed,
    Revision
}
