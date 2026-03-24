using AutoNomX.Domain.Entities;

namespace AutoNomX.Domain.Interfaces;

public interface IChatSessionRepository
{
    Task<ChatSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ChatSession?> GetActiveByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatSession>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<ChatSession> AddAsync(ChatSession session, CancellationToken ct = default);
    Task UpdateAsync(ChatSession session, CancellationToken ct = default);
}
