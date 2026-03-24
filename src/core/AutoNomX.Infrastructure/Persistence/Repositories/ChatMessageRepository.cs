using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoNomX.Infrastructure.Persistence.Repositories;

public class ChatMessageRepository(AutoNomXDbContext context) : IChatMessageRepository
{
    public async Task<IReadOnlyList<ChatMessage>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
        => await context.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

    public async Task<ChatMessage> AddAsync(ChatMessage message, CancellationToken ct = default)
    {
        await context.ChatMessages.AddAsync(message, ct);
        return message;
    }
}
