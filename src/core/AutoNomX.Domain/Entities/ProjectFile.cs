namespace AutoNomX.Domain.Entities;

/// <summary>
/// Proje dosya takibi.
/// DB tablo: files
/// </summary>
public class ProjectFile : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public required string Path { get; set; }
    public string? ContentHash { get; set; }
    public string? LockedByWorker { get; set; }
    public DateTime? LockedAt { get; set; }

    // Navigation
    public Project Project { get; set; } = null!;
    public TaskItem? Task { get; set; }
}
