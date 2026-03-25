using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoNomX.Infrastructure.Persistence.Repositories;

/// <summary>EF Core repository implementation for agent definition entities.</summary>
public class AgentRepository(AutoNomXDbContext context) : IAgentRepository
{
    public async Task<AgentDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Agents.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<AgentDefinition?> GetByNameAsync(string name, CancellationToken ct = default)
        => await context.Agents.FirstOrDefaultAsync(a => a.Name == name, ct);

    public async Task<IReadOnlyList<AgentDefinition>> GetByTypeAsync(AgentType type, CancellationToken ct = default)
        => await context.Agents
            .Where(a => a.Type == type && a.IsActive)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AgentDefinition>> GetAllActiveAsync(CancellationToken ct = default)
        => await context.Agents
            .Where(a => a.IsActive)
            .OrderBy(a => a.Type)
            .ToListAsync(ct);

    public async Task<AgentDefinition> AddAsync(AgentDefinition agent, CancellationToken ct = default)
    {
        await context.Agents.AddAsync(agent, ct);
        return agent;
    }

    public Task UpdateAsync(AgentDefinition agent, CancellationToken ct = default)
    {
        context.Agents.Update(agent);
        return Task.CompletedTask;
    }
}
