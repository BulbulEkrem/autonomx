using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoNomX.Infrastructure.Persistence.Repositories;

/// <summary>EF Core repository implementation for agent history entities.</summary>
public class AgentHistoryRepository(AutoNomXDbContext context) : IAgentHistoryRepository
{
    public async Task<IReadOnlyList<AgentHistory>> GetByAgentIdAsync(Guid agentId, CancellationToken ct = default)
        => await context.AgentHistories
            .Where(h => h.AgentId == agentId)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AgentHistory>> GetByTaskIdAsync(Guid taskId, CancellationToken ct = default)
        => await context.AgentHistories
            .Where(h => h.TaskId == taskId)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AgentHistory>> GetByInstanceIdAsync(string instanceId, CancellationToken ct = default)
        => await context.AgentHistories
            .Where(h => h.AgentInstanceId == instanceId)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(ct);

    public async Task<AgentHistory> AddAsync(AgentHistory history, CancellationToken ct = default)
    {
        await context.AgentHistories.AddAsync(history, ct);
        return history;
    }
}
