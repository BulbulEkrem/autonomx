namespace AutoNomX.Domain.Entities;

/// <summary>
/// Teknik görevler + Kanban board bilgisi.
/// DB tablo: tasks + task_board birleşik
/// </summary>
public class TaskItem : BaseEntity
{
    public Guid ProjectId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Ready;
    public TaskItemPriority Priority { get; set; } = TaskItemPriority.Should;
    public string? AssignedAgent { get; set; }
    public string? AssignedWorker { get; set; }
    public string? GitBranch { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public List<string> Dependencies { get; set; } = [];
    public List<string> FilesTouched { get; set; } = [];
    public List<string> LockedFiles { get; set; } = [];

    // Navigation
    public Project Project { get; set; } = null!;
    public List<AgentHistory> AgentHistories { get; set; } = [];
    public List<ProjectFile> Files { get; set; } = [];
}
