using System.Text.Json;

namespace AutoNomX.Domain.Entities;

/// <summary>
/// Product Owner değişiklik geçmişi.
/// DB tablo: change_log
/// </summary>
public class ChangeLog : BaseEntity
{
    public Guid ProjectId { get; set; }
    public ChangeType ChangeType { get; set; }
    public required string UserMessage { get; set; }
    public string? AgentResponse { get; set; }
    public JsonDocument? Decisions { get; set; }
    public bool IsApproved { get; set; }

    // Navigation
    public Project Project { get; set; } = null!;
}
