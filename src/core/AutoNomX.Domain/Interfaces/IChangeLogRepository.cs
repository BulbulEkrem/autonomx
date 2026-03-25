using AutoNomX.Domain.Entities;

namespace AutoNomX.Domain.Interfaces;

/// <summary>Repository for managing project change log entries.</summary>
public interface IChangeLogRepository
{
    Task<ChangeLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ChangeLog>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<ChangeLog> AddAsync(ChangeLog log, CancellationToken ct = default);
    Task<int> GetChangeCountAsync(Guid projectId, CancellationToken ct = default);
}
