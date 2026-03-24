using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoNomX.Infrastructure.Persistence.Repositories;

public class ChangeLogRepository(AutoNomXDbContext context) : IChangeLogRepository
{
    public async Task<ChangeLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.ChangeLogs.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<ChangeLog>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await context.ChangeLogs
            .Where(c => c.ProjectId == projectId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<ChangeLog> AddAsync(ChangeLog log, CancellationToken ct = default)
    {
        await context.ChangeLogs.AddAsync(log, ct);
        return log;
    }

    public async Task<int> GetChangeCountAsync(Guid projectId, CancellationToken ct = default)
        => await context.ChangeLogs.CountAsync(c => c.ProjectId == projectId, ct);
}
