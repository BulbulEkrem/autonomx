using AutoNomX.Domain.Entities;

namespace AutoNomX.Domain.Interfaces;

public interface IPipelineRunRepository
{
    Task<PipelineRun?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PipelineRun?> GetActiveByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<PipelineRun>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<PipelineRun> AddAsync(PipelineRun run, CancellationToken ct = default);
    Task UpdateAsync(PipelineRun run, CancellationToken ct = default);
}
