using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoNomX.Infrastructure.Persistence.Repositories;

public class CoderWorkerRepository(AutoNomXDbContext context) : ICoderWorkerRepository
{
    public async Task<CoderWorker?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.CoderWorkers
            .Include(w => w.CurrentTask)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task<IReadOnlyList<CoderWorker>> GetAllAsync(CancellationToken ct = default)
        => await context.CoderWorkers
            .Include(w => w.CurrentTask)
            .OrderBy(w => w.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CoderWorker>> GetIdleWorkersAsync(CancellationToken ct = default)
        => await context.CoderWorkers
            .Where(w => w.Status == WorkerStatus.Idle)
            .ToListAsync(ct);

    public async Task<CoderWorker> AddAsync(CoderWorker worker, CancellationToken ct = default)
    {
        await context.CoderWorkers.AddAsync(worker, ct);
        return worker;
    }

    public Task UpdateAsync(CoderWorker worker, CancellationToken ct = default)
    {
        context.CoderWorkers.Update(worker);
        return Task.CompletedTask;
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var worker = await context.CoderWorkers.FindAsync([id], ct);
        if (worker is not null)
            context.CoderWorkers.Remove(worker);
    }
}
