using System.Text.Json;

namespace AutoNomX.Domain.Entities;

/// <summary>
/// Agent tanımları ve konfigürasyonları.
/// DB tablo: agents
/// </summary>
public class AgentDefinition : BaseEntity
{
    public required string Name { get; set; }
    public AgentType Type { get; set; }
    public required string Model { get; set; }
    public required string Provider { get; set; }
    public bool IsActive { get; set; } = true;
    public JsonDocument? LlmConfig { get; set; }

    // Navigation
    public List<AgentHistory> Histories { get; set; } = [];
}
