// DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/Services/ConversationService.cs
using HrMcp.Core.Entities;
using HrMcp.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HrMcp.Infrastructure.Persistence.Services;

public sealed class ConversationService(HrDbContext db) : IConversationService
{
    public async Task<IReadOnlyList<ConversationSession>> GetSessionsAsync(string userId, CancellationToken ct = default)
    {
        return await db.ConversationSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task<ConversationSession> CreateSessionAsync(string userId, string firstPrompt, CancellationToken ct = default)
    {
        var name = firstPrompt.Length <= 50 ? firstPrompt : firstPrompt[..50];
        var session = new ConversationSession
        {
            UserId = userId,
            Name = name
        };
        db.ConversationSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<ConversationSession?> GetSessionAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        return await db.ConversationSessions
            .Include(s => s.Turns.OrderBy(t => t.Timestamp))
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);
    }

    public async Task AddTurnAsync(Guid sessionId, string role, string text, CancellationToken ct = default)
    {
        var turn = new ConversationTurn
        {
            SessionId = sessionId,
            Role = role,
            Text = text
        };
        db.ConversationTurns.Add(turn);

        var session = await db.ConversationSessions.FindAsync([sessionId], ct);
        if (session is not null)
            session.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task RenameSessionAsync(Guid sessionId, string userId, string newName, CancellationToken ct = default)
    {
        var session = await db.ConversationSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);
        if (session is not null)
        {
            session.Name = newName;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteSessionAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        var session = await db.ConversationSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);
        if (session is not null)
        {
            db.ConversationSessions.Remove(session);
            await db.SaveChangesAsync(ct);
        }
    }
}
