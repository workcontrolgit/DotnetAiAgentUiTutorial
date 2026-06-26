namespace HrMcp.Core.Entities;

public sealed class ConversationTurn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty;   // "user" or "assistant"
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public ConversationSession Session { get; set; } = default!;
}
