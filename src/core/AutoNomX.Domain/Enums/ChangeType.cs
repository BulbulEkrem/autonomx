namespace AutoNomX.Domain;

/// <summary>
/// PO değişiklik türleri (ARCHITECTURE.md §4.2).
/// </summary>
public enum ChangeType
{
    AddStory,
    ModifyStory,
    RemoveStory,
    ChangePriority,
    PauseProject,
    ChangeScope
}
