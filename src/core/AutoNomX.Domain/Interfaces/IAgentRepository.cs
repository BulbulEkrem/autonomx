using AutoNomX.Domain.Entities;

namespace AutoNomX.Domain.Interfaces;

/// <summary>Repository for managing agent definition entities.</summary>
public interface IAgentRepository
{
    Task<AgentDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AgentDefinition?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<AgentDefinition>> GetByTypeAsync(AgentType type, CancellationToken ct = default);
    Task<IReadOnlyList<AgentDefinition>> GetAllActiveAsync(CancellationToken ct = default);
    Task<AgentDefinition> AddAsync(AgentDefinition agent, CancellationToken ct = default);
    Task UpdateAsync(AgentDefinition agent, CancellationToken ct = default);
}
