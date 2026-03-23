using AutoNomX.Domain.Entities;

namespace AutoNomX.Domain.Interfaces;

public interface IAgentMetricsRepository
{
    Task<AgentMetrics?> GetByAgentAndModelAsync(Guid agentId, string model, CancellationToken ct = default);
    Task<IReadOnlyList<AgentMetrics>> GetByAgentIdAsync(Guid agentId, CancellationToken ct = default);
    Task<AgentMetrics> AddOrUpdateAsync(AgentMetrics metrics, CancellationToken ct = default);
}
