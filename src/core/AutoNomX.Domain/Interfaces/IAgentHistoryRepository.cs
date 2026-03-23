using AutoNomX.Domain.Entities;

namespace AutoNomX.Domain.Interfaces;

public interface IAgentHistoryRepository
{
    Task<IReadOnlyList<AgentHistory>> GetByAgentIdAsync(Guid agentId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentHistory>> GetByTaskIdAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentHistory>> GetByInstanceIdAsync(string instanceId, CancellationToken ct = default);
    Task<AgentHistory> AddAsync(AgentHistory history, CancellationToken ct = default);
}
