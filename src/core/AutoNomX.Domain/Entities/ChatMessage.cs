namespace AutoNomX.Domain.Entities;

/// <summary>
/// PO Chat mesajı.
/// DB tablo: chat_messages
/// </summary>
public class ChatMessage : BaseEntity
{
    public Guid SessionId { get; set; }
    public required string Role { get; set; }  // "user" or "assistant"
    public required string Content { get; set; }
    public string? ChangeType { get; set; }     // null if no change proposed
    public bool? IsApproved { get; set; }       // null if no approval needed

    // Navigation
    public ChatSession Session { get; set; } = null!;
}
