using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoNomX.Infrastructure.Persistence.Repositories;

public class ProjectFileRepository(AutoNomXDbContext context) : IProjectFileRepository
{
    public async Task<IReadOnlyList<ProjectFile>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await context.ProjectFiles
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.Path)
            .ToListAsync(ct);

    public async Task<ProjectFile?> GetByPathAsync(Guid projectId, string path, CancellationToken ct = default)
        => await context.ProjectFiles
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Path == path, ct);

    public async Task<IReadOnlyList<ProjectFile>> GetLockedFilesAsync(Guid projectId, CancellationToken ct = default)
        => await context.ProjectFiles
            .Where(f => f.ProjectId == projectId && f.LockedByWorker != null)
            .ToListAsync(ct);

    public async Task<ProjectFile> AddAsync(ProjectFile file, CancellationToken ct = default)
    {
        await context.ProjectFiles.AddAsync(file, ct);
        return file;
    }

    public Task UpdateAsync(ProjectFile file, CancellationToken ct = default)
    {
        context.ProjectFiles.Update(file);
        return Task.CompletedTask;
    }

    public async Task<bool> TryAcquireLockAsync(Guid projectId, string path, string workerId, CancellationToken ct = default)
    {
        var file = await GetByPathAsync(projectId, path, ct);

        if (file is null)
        {
            file = new ProjectFile
            {
                ProjectId = projectId,
                Path = path,
                LockedByWorker = workerId,
                LockedAt = DateTime.UtcNow
            };
            await context.ProjectFiles.AddAsync(file, ct);
            return true;
        }

        if (file.LockedByWorker is not null && file.LockedByWorker != workerId)
            return false;

        file.LockedByWorker = workerId;
        file.LockedAt = DateTime.UtcNow;
        return true;
    }

    public async Task ReleaseLockAsync(Guid projectId, string path, CancellationToken ct = default)
    {
        var file = await GetByPathAsync(projectId, path, ct);
        if (file is null) return;

        file.LockedByWorker = null;
        file.LockedAt = null;
    }
}
