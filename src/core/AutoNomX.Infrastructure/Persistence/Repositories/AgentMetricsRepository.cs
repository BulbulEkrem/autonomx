using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoNomX.Infrastructure.Persistence.Repositories;

public class AgentMetricsRepository(AutoNomXDbContext context) : IAgentMetricsRepository
{
    public async Task<AgentMetrics?> GetByAgentAndModelAsync(Guid agentId, string model, CancellationToken ct = default)
        => await context.AgentMetrics
            .FirstOrDefaultAsync(m => m.AgentId == agentId && m.ModelUsed == model, ct);

    public async Task<IReadOnlyList<AgentMetrics>> GetByAgentIdAsync(Guid agentId, CancellationToken ct = default)
        => await context.AgentMetrics
            .Where(m => m.AgentId == agentId)
            .OrderByDescending(m => m.TotalExecutions)
            .ToListAsync(ct);

    public async Task<AgentMetrics> AddOrUpdateAsync(AgentMetrics metrics, CancellationToken ct = default)
    {
        var existing = await GetByAgentAndModelAsync(metrics.AgentId, metrics.ModelUsed, ct);

        if (existing is null)
        {
            await context.AgentMetrics.AddAsync(metrics, ct);
            return metrics;
        }

        existing.AvgIterations = metrics.AvgIterations;
        existing.AvgScore = metrics.AvgScore;
        existing.TotalExecutions = metrics.TotalExecutions;
        existing.SuccessCount = metrics.SuccessCount;
        existing.FailureCount = metrics.FailureCount;
        existing.TotalTokensUsed = metrics.TotalTokensUsed;
        existing.AvgDurationSeconds = metrics.AvgDurationSeconds;

        return existing;
    }
}
