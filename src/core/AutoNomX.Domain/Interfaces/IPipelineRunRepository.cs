using AutoNomX.Domain.Entities;

namespace AutoNomX.Domain.Interfaces;

/// <summary>Repository for managing pipeline run records.</summary>
public interface IPipelineRunRepository
{
    /// <summary>Gets a pipeline run by its unique identifier.</summary>
    Task<PipelineRun?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Gets the currently active pipeline run for a project.</summary>
    Task<PipelineRun?> GetActiveByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    /// <summary>Gets all pipeline runs for the specified project.</summary>
    Task<IReadOnlyList<PipelineRun>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    /// <summary>Adds a new pipeline run.</summary>
    Task<PipelineRun> AddAsync(PipelineRun run, CancellationToken ct = default);
    /// <summary>Updates an existing pipeline run.</summary>
    Task UpdateAsync(PipelineRun run, CancellationToken ct = default);
}
