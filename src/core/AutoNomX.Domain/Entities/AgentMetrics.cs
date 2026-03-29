namespace AutoNomX.Domain.Entities;

/// <summary>
/// Agent performans metrikleri.
/// DB tablo: agent_metrics
/// </summary>
public class AgentMetrics : BaseEntity
{
    public Guid AgentId { get; set; }
    public required string ModelUsed { get; set; }
    public double AvgIterations { get; set; }
    public double AvgScore { get; set; }
    public int TotalExecutions { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public long TotalTokensUsed { get; set; }
    public double AvgDurationSeconds { get; set; }

    // Navigation
}
