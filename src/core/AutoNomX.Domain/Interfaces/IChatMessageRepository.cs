using AutoNomX.Domain.Entities;

namespace AutoNomX.Domain.Interfaces;

/// <summary>Repository for managing chat messages within sessions.</summary>
public interface IChatMessageRepository
{
    Task<IReadOnlyList<ChatMessage>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<ChatMessage> AddAsync(ChatMessage message, CancellationToken ct = default);
}
