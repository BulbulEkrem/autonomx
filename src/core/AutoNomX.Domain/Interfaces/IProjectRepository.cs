using AutoNomX.Domain.Entities;

namespace AutoNomX.Domain.Interfaces;

/// <summary>Repository for managing project entities.</summary>
public interface IProjectRepository
{
    /// <summary>Gets a project by its unique identifier.</summary>
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Gets all projects.</summary>
    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Adds a new project.</summary>
    Task<Project> AddAsync(Project project, CancellationToken ct = default);
    /// <summary>Updates an existing project.</summary>
    Task UpdateAsync(Project project, CancellationToken ct = default);
}
