using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoNomX.Infrastructure.Persistence.Repositories;

public class ProjectRepository(AutoNomXDbContext context) : IProjectRepository
{
    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Projects
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default)
        => await context.Projects
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<Project> AddAsync(Project project, CancellationToken ct = default)
    {
        await context.Projects.AddAsync(project, ct);
        return project;
    }

    public Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        context.Projects.Update(project);
        return Task.CompletedTask;
    }
}
