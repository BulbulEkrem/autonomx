using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoNomX.Infrastructure.Persistence.Repositories;

public class TaskRepository(AutoNomXDbContext context) : ITaskRepository
{
    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<TaskItem>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await context.Tasks
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> GetByStatusAsync(Guid projectId, TaskItemStatus status, CancellationToken ct = default)
        => await context.Tasks
            .Where(t => t.ProjectId == projectId && t.Status == status)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> GetReadyTasksAsync(Guid projectId, CancellationToken ct = default)
        => await context.Tasks
            .Where(t => t.ProjectId == projectId && t.Status == TaskItemStatus.Ready)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<TaskItem> AddAsync(TaskItem task, CancellationToken ct = default)
    {
        await context.Tasks.AddAsync(task, ct);
        return task;
    }

    public Task UpdateAsync(TaskItem task, CancellationToken ct = default)
    {
        context.Tasks.Update(task);
        return Task.CompletedTask;
    }
}
