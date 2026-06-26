# Design: Conversation History & Login
**Date:** 2026-06-25  
**Project:** DotnetAiAgentUiTutorial — Series 2 (Parts 7 & 8)  
**Scope:** Per-user persistent conversation history (Part 7) + ASP.NET Core Identity login with optional OIDC federation (Part 8)

---

## Overview

Two new blog posts extending Series 2:

- **Part 7 — Conversation History:** Named chat sessions persisted per user in SQL Server, surfaced as a ChatGPT-style session list in the sidebar.
- **Part 8 — Login:** ASP.NET Core Identity (local accounts) with optional external OIDC provider federation, gating all workspace access behind authentication.

Both are code + blog deliverables. Code goes in the existing `DotnetAiAgentUi/` solution. Blogs go in `blogs/series-2-ai-agent-ui/`.

---

## Option Selected

**Option A — Minimal layering.** Entities in `HrMcp.Infrastructure.Persistence`, interface in `HrMcp.Application`, implementation in `HrMcp.Infrastructure.Persistence`, auth scaffolded into `HrMcp.Agent`. No new projects.

---

## Section 1: Data Model

Two new EF entities added to `HrMcp.Infrastructure.Persistence`. One EF migration adds both tables to the existing `HrMcpDb`.

### `ConversationSession`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `UserId` | `string` | FK to `AspNetUsers.Id` |
| `Name` | `string` | User-editable; auto-set from first prompt (max 50 chars) |
| `CreatedAt` | `DateTimeOffset` | |
| `UpdatedAt` | `DateTimeOffset` | Updated on each new turn |
| `Turns` | navigation | → `ConversationTurn` |

### `ConversationTurn`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `SessionId` | `Guid` | FK → `ConversationSession.Id` |
| `Role` | `string` | `"user"` or `"assistant"` |
| `Text` | `string` | Full prompt or response text |
| `Timestamp` | `DateTimeOffset` | |

These persist what the in-memory `ChatTurn` record currently holds, scoped to a session and user.

---

## Section 2: Application Layer

### Interface — `IConversationService`
Location: `HrMcp.Application/Services/IConversationService.cs`

```csharp
public interface IConversationService
{
    Task<IReadOnlyList<ConversationSession>> GetSessionsAsync(string userId, CancellationToken ct);
    Task<ConversationSession> CreateSessionAsync(string userId, string firstPrompt, CancellationToken ct);
    Task<ConversationSession?> GetSessionAsync(Guid sessionId, string userId, CancellationToken ct);
    Task AddTurnAsync(Guid sessionId, string role, string text, CancellationToken ct);
    Task RenameSessionAsync(Guid sessionId, string userId, string newName, CancellationToken ct);
    Task DeleteSessionAsync(Guid sessionId, string userId, CancellationToken ct);
}
```

### Implementation
Location: `HrMcp.Infrastructure.Persistence/Services/ConversationService.cs`

All queries scoped to `userId` — users cannot access each other's sessions.

### `AgentDraftService` change
`SendPromptAsync` gains a `sessionId` (Guid?) parameter. When provided, it loads prior turns from `IConversationService` and passes them as conversation history to `IChatClient`, giving the AI memory across page refreshes.

---

## Section 3: UI (Blazor — Part 7)

### Sidebar — Sessions Panel
- Scrollable list of the current user's named sessions, newest first
- "New Chat" button at the top
- Each session shows its name and `UpdatedAt` timestamp
- Active session highlighted
- Inline rename (click session name to edit)
- Delete button per session (with confirmation)

### `DraftWorkspace.razor` changes
- Route: `/workspace/{SessionId:guid}` (session-scoped) and `/workspace` (new session)
- On load with `SessionId`: fetch turns from `IConversationService`, populate `_turns`
- On first send (no session): call `CreateSessionAsync`, navigate to `/workspace/{newId}`
- On subsequent sends: call `AddTurnAsync` for user turn, then AI turn
- Session name auto-set from first prompt truncated to 50 chars
- "New Chat" navigates to `/workspace` — new session created on first send

---

## Section 4: Authentication (Part 8)

### ASP.NET Core Identity
- Scaffolded into `HrMcp.Agent`
- New `ApplicationUser : IdentityUser` class
- Local user tables (`AspNetUsers`, etc.) added to `HrMcpDb` via EF migration
- All Blazor workspace pages protected with `[Authorize]`
- Unauthenticated users redirected to `/login`

### Pages
| Route | Purpose |
|-------|---------|
| `/login` | Username/password form via `SignInManager` |
| `/register` | New account form (can be disabled via config) |
| `/logout` | Clears auth cookie, redirects to `/login` |

### External OIDC Federation (optional)
- Gated behind existing `Features:EnableOidc` flag
- Adds "Sign in with [Provider]" button on `/login`
- Uses `AddOpenIdConnect` — Duende IdentityServer in dev, swappable to Entra ID / Okta via config
- First external login auto-provisions and links a local `ApplicationUser`

### `userId` in Blazor
- Sourced from `AuthenticationStateProvider` (not `IHttpContextAccessor`, which is unreliable in Blazor Server)
- Injected into `DraftWorkspace` and the Sessions sidebar component

---

## Blog Deliverables

| Post | File | Topic |
|------|------|-------|
| Part 7 | `blogs/series-2-ai-agent-ui/part-7-conversation-history.md` | Entities, `IConversationService`, session sidebar UI, `AgentDraftService` history wiring |
| Part 8 | `blogs/series-2-ai-agent-ui/part-8-login-and-oidc.md` | ASP.NET Core Identity scaffold, login/register/logout pages, optional OIDC federation |

---

## Out of Scope
- Multi-device real-time sync (SignalR push to other tabs)
- Admin UI for managing all users' sessions
- Token refresh / sliding expiry (covered in Part 6)
- Full-text search across conversation history
