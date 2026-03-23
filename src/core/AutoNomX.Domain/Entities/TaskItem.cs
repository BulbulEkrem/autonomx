namespace AutoNomX.Domain.Entities;

public class TaskItem
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Ready;
    public TaskItemPriority Priority { get; set; } = TaskItemPriority.Should;
    public string? AssignedWorker { get; set; }
    public List<string> Dependencies { get; set; } = [];
    public List<string> FilesTouched { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
