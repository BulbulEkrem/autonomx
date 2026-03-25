using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoNomX.Infrastructure.Persistence.Repositories;

/// <summary>EF Core repository implementation for pipeline run entities.</summary>
public class PipelineRunRepository(AutoNomXDbContext context) : IPipelineRunRepository
{
    public async Task<PipelineRun?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.PipelineRuns.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<PipelineRun?> GetActiveByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await context.PipelineRuns
            .Where(r => r.ProjectId == projectId && r.Status == PipelineStatus.Running)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<PipelineRun>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await context.PipelineRuns
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task<PipelineRun> AddAsync(PipelineRun run, CancellationToken ct = default)
    {
        await context.PipelineRuns.AddAsync(run, ct);
        return run;
    }

    public Task UpdateAsync(PipelineRun run, CancellationToken ct = default)
    {
        context.PipelineRuns.Update(run);
        return Task.CompletedTask;
    }
}
