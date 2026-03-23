using System.Text.Json;

namespace AutoNomX.Domain.Entities;

/// <summary>
/// Agent iletişim logu.
/// DB tablo: messages
/// </summary>
public class AgentMessage : BaseEntity
{
    public required string FromAgent { get; set; }
    public required string ToAgent { get; set; }
    public required string EventType { get; set; }
    public JsonDocument? Payload { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
}
