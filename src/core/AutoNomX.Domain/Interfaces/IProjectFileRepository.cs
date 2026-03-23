using AutoNomX.Domain.Entities;

namespace AutoNomX.Domain.Interfaces;

public interface IProjectFileRepository
{
    Task<IReadOnlyList<ProjectFile>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectFile?> GetByPathAsync(Guid projectId, string path, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectFile>> GetLockedFilesAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectFile> AddAsync(ProjectFile file, CancellationToken ct = default);
    Task UpdateAsync(ProjectFile file, CancellationToken ct = default);
    Task<bool> TryAcquireLockAsync(Guid projectId, string path, string workerId, CancellationToken ct = default);
    Task ReleaseLockAsync(Guid projectId, string path, CancellationToken ct = default);
}
