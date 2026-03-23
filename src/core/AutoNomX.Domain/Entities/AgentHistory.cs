using System.Text.Json;

namespace AutoNomX.Domain.Entities;

/// <summary>
/// Her agent'ın ve worker instance'ının konuşma geçmişi.
/// DB tablo: agent_history
/// </summary>
public class AgentHistory : BaseEntity
{
    public Guid AgentId { get; set; }
    public string? AgentInstanceId { get; set; }
    public Guid? TaskId { get; set; }
    public required string Role { get; set; }
    public required string Content { get; set; }
    public string? ModelUsed { get; set; }
    public int TokensUsed { get; set; }

    // Navigation
    public AgentDefinition Agent { get; set; } = null!;
    public TaskItem? Task { get; set; }
}
