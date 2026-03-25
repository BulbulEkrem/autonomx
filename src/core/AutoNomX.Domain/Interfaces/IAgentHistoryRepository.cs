using AutoNomX.Domain.Entities;

namespace AutoNomX.Domain.Interfaces;

/// <summary>Repository for managing agent conversation history records.</summary>
public interface IAgentHistoryRepository
{
    /// <summary>Gets all history entries for the specified agent.</summary>
    Task<IReadOnlyList<AgentHistory>> GetByAgentIdAsync(Guid agentId, CancellationToken ct = default);
    /// <summary>Gets all history entries associated with the specified task.</summary>
    Task<IReadOnlyList<AgentHistory>> GetByTaskIdAsync(Guid taskId, CancellationToken ct = default);
    /// <summary>Gets all history entries for a specific agent instance.</summary>
    Task<IReadOnlyList<AgentHistory>> GetByInstanceIdAsync(string instanceId, CancellationToken ct = default);
    /// <summary>Adds a new agent history entry.</summary>
    Task<AgentHistory> AddAsync(AgentHistory history, CancellationToken ct = default);
}
