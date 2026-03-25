using AutoNomX.Domain.Entities;

namespace AutoNomX.Domain.Interfaces;

/// <summary>Repository for managing task item entities.</summary>
public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByStatusAsync(Guid projectId, TaskItemStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetReadyTasksAsync(Guid projectId, CancellationToken ct = default);
    Task<TaskItem> AddAsync(TaskItem task, CancellationToken ct = default);
    Task UpdateAsync(TaskItem task, CancellationToken ct = default);
}
