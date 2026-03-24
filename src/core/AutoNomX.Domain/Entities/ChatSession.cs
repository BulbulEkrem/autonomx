namespace AutoNomX.Domain.Entities;

/// <summary>
/// PO Chat oturum kaydı.
/// DB tablo: chat_sessions
/// </summary>
public class ChatSession : BaseEntity
{
    public Guid ProjectId { get; set; }
    public bool IsActive { get; set; } = true;
    public int OriginalStoryCount { get; set; }
    public int CurrentStoryCount { get; set; }

    // Navigation
    public Project Project { get; set; } = null!;
    public List<ChatMessage> Messages { get; set; } = [];
}
