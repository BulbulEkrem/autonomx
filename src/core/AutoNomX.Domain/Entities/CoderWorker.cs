namespace AutoNomX.Domain.Entities;

/// <summary>
/// Dinamik coder worker tanımları.
/// DB tablo: coder_workers
/// </summary>
public class CoderWorker : BaseEntity
{
    public required string Name { get; set; }
    public required string Model { get; set; }
    public required string Provider { get; set; }
    public WorkerStatus Status { get; set; } = WorkerStatus.Idle;
    public Guid? CurrentTaskId { get; set; }

    // Navigation
    public TaskItem? CurrentTask { get; set; }
}
