namespace AutoNomX.Domain.Entities;

/// <summary>
/// Pipeline çalıştırma geçmişi.
/// DB tablo: pipeline_runs
/// </summary>
public class PipelineRun : BaseEntity
{
    public Guid ProjectId { get; set; }
    public PipelineStatus Status { get; set; } = PipelineStatus.Pending;
    public required string CurrentStep { get; set; }
    public int CurrentIteration { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    // Navigation
    public Project Project { get; set; } = null!;
}
