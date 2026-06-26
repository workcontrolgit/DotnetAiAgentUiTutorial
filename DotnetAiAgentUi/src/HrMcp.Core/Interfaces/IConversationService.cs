using HrMcp.Core.Entities;

namespace HrMcp.Core.Interfaces;

public interface IConversationService
{
    Task<IReadOnlyList<ConversationSession>> GetSessionsAsync(string userId, CancellationToken ct = default);
    Task<ConversationSession> CreateSessionAsync(string userId, string firstPrompt, CancellationToken ct = default);
    Task<ConversationSession?> GetSessionAsync(Guid sessionId, string userId, CancellationToken ct = default);
    Task AddTurnAsync(Guid sessionId, string role, string text, CancellationToken ct = default);
    Task RenameSessionAsync(Guid sessionId, string userId, string newName, CancellationToken ct = default);
    Task DeleteSessionAsync(Guid sessionId, string userId, CancellationToken ct = default);
}
