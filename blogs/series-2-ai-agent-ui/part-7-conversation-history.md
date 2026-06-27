# Part 7: Conversation History — Persistent Named Sessions

**Series:** [AI Agent UI with Blazor United & .NET 10](preface.md) | **Part 7 of 8**  
**GitHub:** [workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)

---

## Introduction

Six parts in, the Writing Assistant works — but every page refresh wipes the slate. The chat is stateless. There is no history, no way to return to a prior conversation, no memory that persists beyond the current browser tab.

This part fixes that. By the end you will have:

- A `ConversationSession` and `ConversationTurn` table in the same SQL Server database the MCP server already uses
- A `IConversationService` that creates, loads, renames, and deletes sessions, scoped by user ID
- A ChatGPT-style session list in the sidebar — click a past conversation to reload it, double-click to rename, hit ✕ to delete
- The AI agent's `_history` reloaded from the database on each session switch, so responses stay contextually aware
- `DraftWorkspace` wired to create a new session on the first prompt and persist every turn

The userId is stubbed as `"dev-user"` for now — Part 8 replaces it with the real authenticated user from ASP.NET Core Identity.

---

## The Data Model

Two new entities in `HrMcp.Core`:

```csharp
// src/HrMcp.Core/Entities/ConversationSession.cs
public sealed class ConversationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ConversationTurn> Turns { get; set; } = [];
}

// src/HrMcp.Core/Entities/ConversationTurn.cs
public sealed class ConversationTurn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty;   // "user" or "assistant"
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public ConversationSession Session { get; set; } = default!;
}
```

These go in `HrMcp.Core` — the same layer as `Position`, `HiringOrganization`, and `PositionRemuneration`. This is the Clean Architecture rule: domain entities belong in Core, everything else builds outward from them.

---

## `IConversationService`

The interface lives in `HrMcp.Core.Interfaces`, matching the pattern for `IPositionRepository`:

```csharp
// src/HrMcp.Core/Interfaces/IConversationService.cs
public interface IConversationService
{
    Task<IReadOnlyList<ConversationSession>> GetSessionsAsync(string userId, CancellationToken ct = default);
    Task<ConversationSession> CreateSessionAsync(string userId, string firstPrompt, CancellationToken ct = default);
    Task<ConversationSession?> GetSessionAsync(Guid sessionId, string userId, CancellationToken ct = default);
    Task AddTurnAsync(Guid sessionId, string role, string text, CancellationToken ct = default);
    Task RenameSessionAsync(Guid sessionId, string userId, string newName, CancellationToken ct = default);
    Task DeleteSessionAsync(Guid sessionId, string userId, CancellationToken ct = default);
}
```

Every method accepts `userId` and uses it to filter queries — users can never access each other's sessions.

---

## `ConversationService` Implementation

The implementation lives in `HrMcp.Infrastructure.Persistence/Services/ConversationService.cs` alongside `PositionRepository`. Key points:

- `CreateSessionAsync` auto-names the session from the first prompt, truncated to 50 characters
- `GetSessionAsync` uses `.Include(s => s.Turns.OrderBy(t => t.Timestamp))` to load turns in order
- `AddTurnAsync` uses `ExecuteUpdateAsync` to update `UpdatedAt` without loading the session entity
- `DeleteSessionAsync` uses `ExecuteDeleteAsync` with cascade delete — turns are removed automatically

```csharp
// src/HrMcp.Infrastructure.Persistence/Services/ConversationService.cs
public sealed class ConversationService(HrDbContext db) : IConversationService
{
    public async Task<ConversationSession> CreateSessionAsync(string userId, string firstPrompt, CancellationToken ct = default)
    {
        var name = firstPrompt.Length <= 50 ? firstPrompt : firstPrompt[..50];
        var session = new ConversationSession { UserId = userId, Name = name };
        db.ConversationSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session;
    }
    // ... (remaining methods as above)
}
```

---

## Giving the AI Memory

`HrAgent` maintains `_history` — a `List<ChatMessage>` starting with the system prompt. When a session is loaded, we need to pre-populate this list with the prior turns. Add `ResetHistory` to `HrAgent`:

```csharp
public void ResetHistory(IReadOnlyList<ChatMessage> priorMessages)
{
    _history.Clear();
    _history.Add(new ChatMessage(ChatRole.System, SystemPrompt));
    foreach (var msg in priorMessages)
        _history.Add(msg);
}
```

`AgentDraftService.SendPromptAsync` now accepts an optional `sessionId`. When present, it loads the session's turns, converts them to `ChatMessage` objects, and calls `ResetHistory` before forwarding the prompt to the agent:

```csharp
public async Task<string> SendPromptAsync(string prompt, Guid? sessionId = null, CancellationToken ct = default)
{
    await EnsureInitializedAsync(ct);

    if (sessionId.HasValue)
    {
        var session = await _conversationService.GetSessionAsync(sessionId.Value, "dev-user", ct);
        if (session is not null && session.Turns.Count > 0)
        {
            var priorMessages = session.Turns
                .OrderBy(t => t.Timestamp)
                .Select(t => new ChatMessage(
                    string.Equals(t.Role, "user", StringComparison.OrdinalIgnoreCase)
                        ? ChatRole.User : ChatRole.Assistant, t.Text))
                .ToList();
            _agent!.ResetHistory(priorMessages);
        }
    }

    return await _agent!.AskAsync(prompt, ct);
}
```

---

## Session Routing in `DraftWorkspace`

`DraftWorkspace` now handles two routes:

```razor
@page "/"
@page "/workspace/{SessionId:guid}"
```

`/` is "new chat" — no session yet. `/workspace/{id}` loads an existing session. On `OnParametersSetAsync`, if `SessionId` has a value, the component loads the session's turns from the database and populates `_turns`. On the first send at `/`, the component creates a new session and navigates to `/workspace/{newId}`.

---

## Sessions Sidebar

`SessionsSidebar.razor` is a self-contained component that:

- Loads the current user's sessions from `IConversationService.GetSessionsAsync` on init
- Refreshes the list whenever `NavigationManager.LocationChanged` fires
- Highlights the active session by checking if the current URL contains the session ID
- Double-click to rename (inline `<input>`); ✕ button to delete with immediate nav fallback to `/`

```razor
<div class="sessions-sidebar">
    <div class="sessions-header">
        <span class="sessions-title">Conversations</span>
        <button class="ghost-btn ghost-btn--sm" @onclick="NewChat">+ New</button>
    </div>
    <!-- session list -->
</div>
```

---

## Running It

```bash
dotnet run --project DotnetAiAgentUi/src/HrMcp.Agent -- --web
```

Open `http://localhost:5000`. Type a prompt, hit Enter — a named session appears in the sidebar. Refresh the page, navigate back to the session — the full conversation history reloads. The AI remembers the prior context.

---

## What We Built

- `ConversationSession` + `ConversationTurn` entities in `HrMcp.Core`
- `IConversationService` interface and `ConversationService` EF implementation
- EF migration `AddConversationHistory`
- `HrAgent.ResetHistory` for reloading conversation context
- `AgentDraftService` updated to pass session context to the agent
- `DraftWorkspace` wired to create and persist sessions
- `SessionsSidebar` with rename, delete, and New Chat

**Next Up:** [Part 8 — Login & OIDC Federation](part-8-login-and-oidc.md) — replace `"dev-user"` with a real authenticated identity using ASP.NET Core Identity and optional external OIDC.

---

Tags: `dotnet` `blazor` `csharp` `efcore` `ai`
