using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoNomX.Infrastructure.Persistence.Repositories;

public class ChatSessionRepository(AutoNomXDbContext context) : IChatSessionRepository
{
    public async Task<ChatSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.ChatSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<ChatSession?> GetActiveByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await context.ChatSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.IsActive, ct);

    public async Task<IReadOnlyList<ChatSession>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await context.ChatSessions
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public async Task<ChatSession> AddAsync(ChatSession session, CancellationToken ct = default)
    {
        await context.ChatSessions.AddAsync(session, ct);
        return session;
    }

    public Task UpdateAsync(ChatSession session, CancellationToken ct = default)
    {
        context.ChatSessions.Update(session);
        return Task.CompletedTask;
    }
}
