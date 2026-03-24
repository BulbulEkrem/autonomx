using System.Text.Json;

namespace AutoNomX.Domain.Entities;

/// <summary>
/// Proje metadata ve konfigürasyonu.
/// DB tablo: projects
/// </summary>
public class Project : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Created;
    public string? RepositoryPath { get; set; }
    public JsonDocument? Config { get; set; }

    // Navigation
    public List<TaskItem> Tasks { get; set; } = [];
    public List<PipelineRun> PipelineRuns { get; set; } = [];
    public List<ProjectFile> Files { get; set; } = [];
    public List<ChangeLog> ChangeLogs { get; set; } = [];
    public List<ChatSession> ChatSessions { get; set; } = [];
}
