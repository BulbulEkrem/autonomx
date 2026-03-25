using AutoNomX.Domain.Entities;

namespace AutoNomX.Domain.Interfaces;

/// <summary>Repository for managing coder worker entities.</summary>
public interface ICoderWorkerRepository
{
    /// <summary>Gets a coder worker by its unique identifier.</summary>
    Task<CoderWorker?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Gets all coder workers.</summary>
    Task<IReadOnlyList<CoderWorker>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Gets all coder workers currently in idle status.</summary>
    Task<IReadOnlyList<CoderWorker>> GetIdleWorkersAsync(CancellationToken ct = default);
    /// <summary>Adds a new coder worker.</summary>
    Task<CoderWorker> AddAsync(CoderWorker worker, CancellationToken ct = default);
    /// <summary>Updates an existing coder worker.</summary>
    Task UpdateAsync(CoderWorker worker, CancellationToken ct = default);
    /// <summary>Removes a coder worker by its unique identifier.</summary>
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
