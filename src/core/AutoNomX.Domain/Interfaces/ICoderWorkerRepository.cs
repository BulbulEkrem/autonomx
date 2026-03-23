using AutoNomX.Domain.Entities;

namespace AutoNomX.Domain.Interfaces;

public interface ICoderWorkerRepository
{
    Task<CoderWorker?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CoderWorker>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CoderWorker>> GetIdleWorkersAsync(CancellationToken ct = default);
    Task<CoderWorker> AddAsync(CoderWorker worker, CancellationToken ct = default);
    Task UpdateAsync(CoderWorker worker, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
