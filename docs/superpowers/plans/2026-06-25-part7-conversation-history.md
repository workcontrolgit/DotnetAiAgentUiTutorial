# Part 7 — Conversation History Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist chat sessions per user in SQL Server and surface them as a named session list in the sidebar, giving the AI memory across page refreshes.

**Architecture:** Two new EF entities (`ConversationSession`, `ConversationTurn`) in `HrMcp.Core`. `IConversationService` interface in `HrMcp.Core.Interfaces`, implemented in `HrMcp.Infrastructure.Persistence`. `DraftWorkspace` gains a `SessionId` route parameter and calls `IConversationService` directly. `AgentDraftService` reloads prior turns into `HrAgent._history` when given a session. userId is stubbed as `"dev-user"` — Part 8 replaces it with the real authenticated user.

**Tech Stack:** .NET 10, EF Core 9 (SqlServer), Blazor United (InteractiveServer), MudBlazor 8, xUnit + bUnit

## Global Constraints

- Target framework: `net10.0` everywhere
- EF packages: `Microsoft.EntityFrameworkCore.SqlServer` Version `9.*` (matches existing)
- All Guid PKs use `Guid.NewGuid()` defaults in C#; EF maps them as `uniqueidentifier` in SQL Server
- Stub userId constant: `private const string DevUserId = "dev-user";` in `DraftWorkspace`
- All new files follow existing namespace convention: `HrMcp.<Layer>.<Subfolder>`
- No breaking changes to `IAgentDraftService.ExportDraftToWordAsync`
- Build command: `dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx`
- Migration command (run from repo root): `dotnet ef migrations add <Name> --project DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence --startup-project DotnetAiAgentUi/src/HrMcp.Agent`

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `HrMcp.Core/Entities/ConversationSession.cs` | Session entity |
| Create | `HrMcp.Core/Entities/ConversationTurn.cs` | Turn entity |
| Create | `HrMcp.Core/Interfaces/IConversationService.cs` | Service contract |
| Modify | `HrMcp.Infrastructure.Persistence/HrDbContext.cs` | Add DbSets |
| Create | `HrMcp.Infrastructure.Persistence/Services/ConversationService.cs` | EF implementation |
| Modify | `HrMcp.Infrastructure.Persistence/DependencyInjection.cs` | Register service |
| Modify | `HrMcp.Agent/HrAgent.cs` | Add `ResetHistory` method |
| Modify | `HrMcp.Agent/Web/Services/AgentDraftService.cs` | Inject service, reload history |
| Modify | `HrMcp.Agent/HrMcp.Agent.csproj` | Add project references |
| Modify | `HrMcp.Agent/Program.cs` (`RunWebAsync`) | Register persistence |
| Modify | `HrMcp.Agent/Components/Layout/MainLayout.razor` | Add sessions sidebar |
| Modify | `HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Session route + CRUD |
| Create | `HrMcp.Agent/Components/Layout/SessionsSidebar.razor` | Session list component |
| Create | (EF migration) | Add History tables |
| Create | `blogs/series-2-ai-agent-ui/part-7-conversation-history.md` | Blog post |

---

### Task 1: Core Entities

**Files:**
- Create: `DotnetAiAgentUi/src/HrMcp.Core/Entities/ConversationSession.cs`
- Create: `DotnetAiAgentUi/src/HrMcp.Core/Entities/ConversationTurn.cs`
- Create: `DotnetAiAgentUi/src/HrMcp.Core/Interfaces/IConversationService.cs`

**Interfaces:**
- Produces: `ConversationSession`, `ConversationTurn`, `IConversationService` — consumed by Tasks 2, 3, 5, 6

- [ ] **Step 1: Create `ConversationSession` entity**

```csharp
// DotnetAiAgentUi/src/HrMcp.Core/Entities/ConversationSession.cs
namespace HrMcp.Core.Entities;

public sealed class ConversationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ConversationTurn> Turns { get; set; } = [];
}
```

- [ ] **Step 2: Create `ConversationTurn` entity**

```csharp
// DotnetAiAgentUi/src/HrMcp.Core/Entities/ConversationTurn.cs
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
```

- [ ] **Step 3: Create `IConversationService` interface**

```csharp
// DotnetAiAgentUi/src/HrMcp.Core/Interfaces/IConversationService.cs
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
```

- [ ] **Step 4: Build to confirm no errors**

```bash
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Core/Entities/ConversationSession.cs \
        DotnetAiAgentUi/src/HrMcp.Core/Entities/ConversationTurn.cs \
        DotnetAiAgentUi/src/HrMcp.Core/Interfaces/IConversationService.cs
git commit -m "feat(core): add ConversationSession, ConversationTurn entities and IConversationService"
```

---

### Task 2: Infrastructure — DbContext + ConversationService

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/HrDbContext.cs`
- Create: `DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/Services/ConversationService.cs`
- Modify: `DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/DependencyInjection.cs`

**Interfaces:**
- Consumes: `ConversationSession`, `ConversationTurn`, `IConversationService` from Task 1
- Produces: `ConversationService` — registered as `IConversationService` in DI

- [ ] **Step 1: Write failing test for `ConversationService.CreateSessionAsync`**

```csharp
// DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ConversationServiceTests.cs
using HrMcp.Core.Interfaces;
using HrMcp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrMcp.Agent.Tests;

public sealed class ConversationServiceTests : IDisposable
{
    private readonly HrDbContext _db;
    private readonly IConversationService _sut;

    public ConversationServiceTests()
    {
        var options = new DbContextOptionsBuilder<HrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new HrDbContext(options);
        _sut = new HrMcp.Infrastructure.Persistence.Services.ConversationService(_db);
    }

    [Fact]
    public async Task CreateSessionAsync_StoresSessionWithTruncatedName()
    {
        var session = await _sut.CreateSessionAsync("user1", "Draft a job description for a software engineer role", default);

        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal("user1", session.UserId);
        Assert.True(session.Name.Length <= 50);
        Assert.StartsWith("Draft a job description", session.Name);
    }

    [Fact]
    public async Task GetSessionsAsync_ReturnsOnlyUserSessions()
    {
        await _sut.CreateSessionAsync("user1", "First session", default);
        await _sut.CreateSessionAsync("user2", "Other user session", default);

        var sessions = await _sut.GetSessionsAsync("user1", default);

        Assert.Single(sessions);
        Assert.Equal("user1", sessions[0].UserId);
    }

    [Fact]
    public async Task AddTurnAsync_AppendsTurnToSession()
    {
        var session = await _sut.CreateSessionAsync("user1", "Hello", default);

        await _sut.AddTurnAsync(session.Id, "user", "Hello", default);
        await _sut.AddTurnAsync(session.Id, "assistant", "Hi there!", default);

        var loaded = await _sut.GetSessionAsync(session.Id, "user1", default);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Turns.Count);
    }

    [Fact]
    public async Task GetSessionAsync_ReturnsNullForWrongUser()
    {
        var session = await _sut.CreateSessionAsync("user1", "My session", default);

        var result = await _sut.GetSessionAsync(session.Id, "user2", default);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteSessionAsync_RemovesSession()
    {
        var session = await _sut.CreateSessionAsync("user1", "To delete", default);

        await _sut.DeleteSessionAsync(session.Id, "user1", default);

        var result = await _sut.GetSessionAsync(session.Id, "user1", default);
        Assert.Null(result);
    }

    public void Dispose() => _db.Dispose();
}
```

- [ ] **Step 2: Add EF InMemory package to test project**

```bash
dotnet add DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj package Microsoft.EntityFrameworkCore.InMemory --version 9.*
```

- [ ] **Step 3: Run tests to confirm they fail (type not found)**

```bash
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ --filter "ConversationServiceTests"
```
Expected: Build error — `ConversationService` does not exist yet.

- [ ] **Step 4: Update `HrDbContext` to add new DbSets**

Replace the full `HrDbContext.cs` with:

```csharp
// DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/HrDbContext.cs
using HrMcp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrMcp.Infrastructure.Persistence;

public class HrDbContext(DbContextOptions<HrDbContext> options) : DbContext(options)
{
    public DbSet<HiringOrganization>   HiringOrganizations   => Set<HiringOrganization>();
    public DbSet<Position>             Positions             => Set<Position>();
    public DbSet<PositionRemuneration> PositionRemunerations => Set<PositionRemuneration>();
    public DbSet<ConversationSession>  ConversationSessions  => Set<ConversationSession>();
    public DbSet<ConversationTurn>     ConversationTurns     => Set<ConversationTurn>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PositionRemuneration>()
            .Property(r => r.MinimumRange).HasPrecision(18, 2);
        modelBuilder.Entity<PositionRemuneration>()
            .Property(r => r.MaximumRange).HasPrecision(18, 2);

        modelBuilder.Entity<PositionRemuneration>()
            .HasOne(r => r.Position)
            .WithOne(p => p.PositionRemuneration)
            .HasForeignKey<PositionRemuneration>(r => r.PositionId);

        modelBuilder.Entity<ConversationSession>()
            .HasIndex(s => s.UserId);

        modelBuilder.Entity<ConversationTurn>()
            .HasOne(t => t.Session)
            .WithMany(s => s.Turns)
            .HasForeignKey(t => t.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 5: Create `ConversationService` implementation**

```csharp
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

        await db.ConversationSessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);

        await db.SaveChangesAsync(ct);
    }

    public async Task RenameSessionAsync(Guid sessionId, string userId, string newName, CancellationToken ct = default)
    {
        await db.ConversationSessions
            .Where(s => s.Id == sessionId && s.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, newName), ct);
    }

    public async Task DeleteSessionAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        await db.ConversationSessions
            .Where(s => s.Id == sessionId && s.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }
}
```

- [ ] **Step 6: Register `ConversationService` in `DependencyInjection.cs`**

```csharp
// DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/DependencyInjection.cs
using HrMcp.Core.Interfaces;
using HrMcp.Infrastructure.Persistence.Repositories;
using HrMcp.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrMcp.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<HrDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IHiringOrganizationRepository, HiringOrganizationRepository>();
        services.AddScoped<IConversationService, ConversationService>();

        return services;
    }
}
```

- [ ] **Step 7: Run tests — should pass**

```bash
dotnet test DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ --filter "ConversationServiceTests"
```
Expected: 5 tests pass.

- [ ] **Step 8: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/ \
        DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ConversationServiceTests.cs
git commit -m "feat(persistence): add ConversationService with EF implementation and tests"
```

---

### Task 3: Add Project References + Register Persistence in Agent

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Program.cs` (`RunWebAsync` function)
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/appsettings.json`

**Interfaces:**
- Consumes: `AddPersistence` extension from Task 2
- Produces: `IConversationService` available in DI for Tasks 4 and 5

- [ ] **Step 1: Add project references to `HrMcp.Agent.csproj`**

Add inside the existing `<ItemGroup>` with `<PackageReference>` entries (or add a new `<ItemGroup>`):

```xml
  <ItemGroup>
    <ProjectReference Include="..\HrMcp.Core\HrMcp.Core.csproj" />
    <ProjectReference Include="..\HrMcp.Application\HrMcp.Application.csproj" />
    <ProjectReference Include="..\HrMcp.Infrastructure.Persistence\HrMcp.Infrastructure.Persistence.csproj" />
  </ItemGroup>
```

- [ ] **Step 2: Add connection string to `appsettings.json`**

Open `DotnetAiAgentUi/src/HrMcp.Agent/appsettings.json` and add alongside the existing keys:

```json
"ConnectionStrings": {
  "HrDb": "Server=(localdb)\\mssqllocaldb;Database=HrMcpDb;Trusted_Connection=True;"
}
```

- [ ] **Step 3: Register persistence in `RunWebAsync`**

In `Program.cs`, update the `RunWebAsync` static function. Find:

```csharp
static async Task RunWebAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.UseStaticWebAssets();
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
    builder.Services.AddScoped<IAgentDraftService, AgentDraftService>();
```

Replace with:

```csharp
static async Task RunWebAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.UseStaticWebAssets();
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    var connectionString = builder.Configuration.GetConnectionString("HrDb")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:HrDb");
    builder.Services.AddPersistence(connectionString);
    builder.Services.AddScoped<IAgentDraftService, AgentDraftService>();
```

Also add the using at the top of `Program.cs`:

```csharp
using HrMcp.Infrastructure.Persistence;
```

- [ ] **Step 4: Build to confirm**

```bash
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj \
        DotnetAiAgentUi/src/HrMcp.Agent/Program.cs \
        DotnetAiAgentUi/src/HrMcp.Agent/appsettings.json
git commit -m "feat(agent): add project references and register persistence in web host"
```

---

### Task 4: EF Migration

**Files:**
- Create: EF migration (auto-generated)

**Interfaces:**
- Consumes: `HrDbContext` with new DbSets from Task 2
- Produces: SQL Server tables `ConversationSessions`, `ConversationTurns`

- [ ] **Step 1: Run EF migration from repo root**

```bash
dotnet ef migrations add AddConversationHistory \
  --project DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence \
  --startup-project DotnetAiAgentUi/src/HrMcp.Agent
```
Expected: Migration file created in `DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/Migrations/`.

- [ ] **Step 2: Apply migration**

```bash
dotnet ef database update \
  --project DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence \
  --startup-project DotnetAiAgentUi/src/HrMcp.Agent
```
Expected: `Done. Applied migration 'AddConversationHistory'.`

- [ ] **Step 3: Commit migration files**

```bash
git add DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/Migrations/
git commit -m "feat(migrations): add AddConversationHistory migration"
```

---

### Task 5: Update `HrAgent` — Add `ResetHistory`

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs`

**Interfaces:**
- Consumes: `Microsoft.Extensions.AI.ChatMessage`, `Microsoft.Extensions.AI.ChatRole`
- Produces: `HrAgent.ResetHistory(IReadOnlyList<ChatMessage> priorMessages)` — consumed by Task 6

- [ ] **Step 1: Add `ResetHistory` method to `HrAgent`**

In `HrAgent.cs`, find the private `_history` field:

```csharp
private readonly List<ChatMessage> _history =
[
    new(ChatRole.System, SystemPrompt)
];
```

Add the public method directly after:

```csharp
public void ResetHistory(IReadOnlyList<ChatMessage> priorMessages)
{
    _history.Clear();
    _history.Add(new ChatMessage(ChatRole.System, SystemPrompt));
    foreach (var msg in priorMessages)
        _history.Add(msg);
}
```

- [ ] **Step 2: Build to confirm**

```bash
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/HrAgent.cs
git commit -m "feat(agent): add ResetHistory to HrAgent for session-scoped conversation context"
```

---

### Task 6: Update `AgentDraftService` — Inject `IConversationService`, Add `sessionId`

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/AgentDraftService.cs`

**Interfaces:**
- Consumes: `IConversationService.GetSessionAsync` from Task 2; `HrAgent.ResetHistory` from Task 5
- Produces: `IAgentDraftService.SendPromptAsync(string prompt, Guid? sessionId, CancellationToken ct)` — consumed by Task 7

- [ ] **Step 1: Update `IAgentDraftService` interface**

In `AgentDraftService.cs`, replace the interface declaration:

```csharp
public interface IAgentDraftService
{
    Task<string> SendPromptAsync(string prompt, Guid? sessionId = null, CancellationToken ct = default);
    Task<(string Message, string? FileName, byte[]? FileBytes)> ExportDraftToWordAsync(string draftText, CancellationToken ct = default);
}
```

- [ ] **Step 2: Add constructor injection for `IConversationService`**

Replace the class opening (the existing field declarations at the top of `AgentDraftService`):

```csharp
public sealed class AgentDraftService : IAgentDraftService, IAsyncDisposable
{
    private const int DefaultExportContextPositionId = 1;

    private HrAgent? _agent;
    private McpClient? _mcpClient;
    private IConfiguration? _configuration;
```

With:

```csharp
public sealed class AgentDraftService : IAgentDraftService, IAsyncDisposable
{
    private const int DefaultExportContextPositionId = 1;

    private readonly IConversationService _conversationService;
    private HrAgent? _agent;
    private McpClient? _mcpClient;
    private IConfiguration? _configuration;

    public AgentDraftService(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }
```

Also add the using at the top of the file:

```csharp
using HrMcp.Core.Interfaces;
using Microsoft.Extensions.AI;
```

- [ ] **Step 3: Update `SendPromptAsync` to reload history**

Replace the existing `SendPromptAsync` implementation:

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
                        ? ChatRole.User
                        : ChatRole.Assistant,
                    t.Text))
                .ToList();
            _agent!.ResetHistory(priorMessages);
        }
    }

    return await _agent!.AskAsync(prompt, ct);
}
```

- [ ] **Step 4: Build to confirm**

```bash
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/AgentDraftService.cs
git commit -m "feat(service): inject IConversationService into AgentDraftService, pass sessionId for history reload"
```

---

### Task 7: `DraftWorkspace` — Session Route + CRUD

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`

**Interfaces:**
- Consumes: `IConversationService` (Tasks 1–2), `IAgentDraftService.SendPromptAsync(prompt, sessionId)` (Task 6)
- Produces: navigates to `/workspace/{SessionId}`, creates/loads sessions

- [ ] **Step 1: Update `DraftWorkspace.razor` routes, injects, and session fields**

At the top of `DraftWorkspace.razor`, replace:

```razor
@page "/"
@using HrMcp.Agent.Web.Models
@using HrMcp.Agent.Web.Services
@using Markdig
@using Microsoft.AspNetCore.Components.Web
@using System.Text
@using System.Text.RegularExpressions
@inject IAgentDraftService AgentDraftService
@inject IJSRuntime JS
```

With:

```razor
@page "/"
@page "/workspace/{SessionId:guid}"
@using HrMcp.Agent.Web.Models
@using HrMcp.Agent.Web.Services
@using HrMcp.Core.Interfaces
@using Markdig
@using Microsoft.AspNetCore.Components.Web
@using System.Text
@using System.Text.RegularExpressions
@inject IAgentDraftService AgentDraftService
@inject IConversationService ConversationService
@inject NavigationManager Nav
@inject IJSRuntime JS
```

- [ ] **Step 2: Add `SessionId` parameter and `DevUserId` constant in `@code` block**

In the `@code` block, after `private string WorkspaceGridStyle => ...`, add:

```csharp
[Parameter] public Guid? SessionId { get; set; }

private const string DevUserId = "dev-user";
```

- [ ] **Step 3: Add `OnParametersSetAsync` to load session turns**

Add after the `OnAfterRenderAsync` override:

```csharp
protected override async Task OnParametersSetAsync()
{
    if (SessionId is null)
    {
        _turns.Clear();
        return;
    }

    var session = await ConversationService.GetSessionAsync(SessionId.Value, DevUserId);
    if (session is null)
    {
        Nav.NavigateTo("/");
        return;
    }

    _turns.Clear();
    foreach (var turn in session.Turns.OrderBy(t => t.Timestamp))
    {
        var role = string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase) ? "You" : "Assistant";
        _turns.Add(new ChatTurn(role, turn.Text, turn.Timestamp));
    }
}
```

- [ ] **Step 4: Update `SendPromptAsync` to persist turns**

Replace the existing `SendPromptAsync` method body with:

```csharp
private async Task SendPromptAsync()
{
    if (_busy || string.IsNullOrWhiteSpace(Prompt))
        return;

    _busy = true;
    _status = string.Empty;
    var input = Prompt.Trim();
    Prompt = string.Empty;

    // Create a new session on the first turn
    if (SessionId is null)
    {
        var newSession = await ConversationService.CreateSessionAsync(DevUserId, input);
        SessionId = newSession.Id;
        Nav.NavigateTo($"/workspace/{SessionId}", forceLoad: false);
    }

    _turns.Add(new ChatTurn("You", input, DateTimeOffset.UtcNow));
    await ConversationService.AddTurnAsync(SessionId.Value, "user", input);

    try
    {
        var response = await AgentDraftService.SendPromptAsync(input, SessionId);
        _turns.Add(new ChatTurn("Assistant", response, DateTimeOffset.UtcNow));
        await ConversationService.AddTurnAsync(SessionId.Value, "assistant", response);

        if (ShouldSyncDraft(input, response))
        {
            var draftMarkdown = ExtractDraftMarkdown(response);
            if (draftMarkdown is not null)
            {
                var html = NormalizeHtmlForQuill(Markdown.ToHtml(draftMarkdown, ChatMarkdownPipeline));
                if (!_draftVisible)
                {
                    _pendingHtml = html;
                    _draftVisible = true;
                }
                else
                {
                    await JS.InvokeVoidAsync("setQuillContent", "quill-editor-wrapper", html);
                }
            }
        }
    }
    catch (Exception ex)
    {
        _status = $"Error: {ex.Message}";
    }
    finally
    {
        _busy = false;
    }
}
```

- [ ] **Step 5: Build to confirm**

```bash
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor
git commit -m "feat(ui): add session route and persistence to DraftWorkspace"
```

---

### Task 8: Sessions Sidebar Component

**Files:**
- Create: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Layout/SessionsSidebar.razor`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Layout/MainLayout.razor`

**Interfaces:**
- Consumes: `IConversationService.GetSessionsAsync`, `IConversationService.RenameSessionAsync`, `IConversationService.DeleteSessionAsync`
- Produces: Session list UI in sidebar; `New Chat` button navigates to `/`

- [ ] **Step 1: Create `SessionsSidebar.razor`**

```razor
@* DotnetAiAgentUi/src/HrMcp.Agent/Components/Layout/SessionsSidebar.razor *@
@using HrMcp.Core.Entities
@using HrMcp.Core.Interfaces
@inject IConversationService ConversationService
@inject NavigationManager Nav
@implements IDisposable

<div class="sessions-sidebar">
    <div class="sessions-header">
        <span class="sessions-title">Conversations</span>
        <button class="ghost-btn ghost-btn--sm" @onclick="NewChat" title="New Chat">+ New</button>
    </div>

    @if (_sessions.Count == 0)
    {
        <div class="sessions-empty">No conversations yet.</div>
    }
    else
    {
        <ul class="sessions-list">
            @foreach (var session in _sessions)
            {
                var isActive = Nav.Uri.Contains(session.Id.ToString(), StringComparison.OrdinalIgnoreCase);
                <li class="session-item @(isActive ? "session-item--active" : "")" @key="session.Id">
                    @if (_renamingId == session.Id)
                    {
                        <input class="session-rename-input"
                               @bind="_renameValue"
                               @onblur="() => CommitRename(session.Id)"
                               @onkeydown="e => HandleRenameKey(e, session.Id)"
                               @ref="_renameInput" />
                    }
                    else
                    {
                        <span class="session-name" @onclick="() => OpenSession(session.Id)"
                              @ondblclick="() => StartRename(session)">
                            @session.Name
                        </span>
                        <button class="ghost-btn ghost-btn--icon" @onclick="() => DeleteSession(session.Id)"
                                title="Delete">✕</button>
                    }
                </li>
            }
        </ul>
    }
</div>

@code {
    private const string DevUserId = "dev-user";
    private List<ConversationSession> _sessions = [];
    private Guid? _renamingId;
    private string _renameValue = string.Empty;
    private ElementReference _renameInput;

    protected override async Task OnInitializedAsync()
    {
        await LoadSessionsAsync();
        Nav.LocationChanged += OnLocationChanged;
    }

    private async void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        await LoadSessionsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadSessionsAsync()
    {
        _sessions = (await ConversationService.GetSessionsAsync(DevUserId)).ToList();
    }

    private void NewChat() => Nav.NavigateTo("/");

    private void OpenSession(Guid id) => Nav.NavigateTo($"/workspace/{id}");

    private void StartRename(ConversationSession session)
    {
        _renamingId = session.Id;
        _renameValue = session.Name;
    }

    private async Task CommitRename(Guid id)
    {
        if (!string.IsNullOrWhiteSpace(_renameValue))
            await ConversationService.RenameSessionAsync(id, DevUserId, _renameValue.Trim());
        _renamingId = null;
        await LoadSessionsAsync();
    }

    private async Task HandleRenameKey(KeyboardEventArgs e, Guid id)
    {
        if (e.Key == "Enter") await CommitRename(id);
        if (e.Key == "Escape") _renamingId = null;
    }

    private async Task DeleteSession(Guid id)
    {
        await ConversationService.DeleteSessionAsync(id, DevUserId);
        if (Nav.Uri.Contains(id.ToString(), StringComparison.OrdinalIgnoreCase))
            Nav.NavigateTo("/");
        await LoadSessionsAsync();
    }

    public void Dispose() => Nav.LocationChanged -= OnLocationChanged;
}
```

- [ ] **Step 2: Update `MainLayout.razor` to include sidebar**

Replace the full `MainLayout.razor`:

```razor
@inherits LayoutComponentBase

<div class="main-layout">
    <SessionsSidebar />
    <div class="main-content">
        @Body
    </div>
</div>
```

- [ ] **Step 3: Add sidebar CSS to `app.css` or `site.css`**

Open `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css` and append:

```css
/* Sessions Sidebar */
.main-layout {
    display: flex;
    height: 100vh;
}

.sessions-sidebar {
    width: 240px;
    min-width: 200px;
    background: #1e1e2e;
    color: #cdd6f4;
    display: flex;
    flex-direction: column;
    padding: 0.75rem 0.5rem;
    border-right: 1px solid #313244;
    overflow-y: auto;
}

.sessions-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0 0.5rem 0.75rem;
    border-bottom: 1px solid #313244;
    margin-bottom: 0.5rem;
}

.sessions-title {
    font-size: 0.75rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: #a6adc8;
}

.sessions-empty {
    font-size: 0.8rem;
    color: #585b70;
    padding: 0.5rem;
    text-align: center;
}

.sessions-list {
    list-style: none;
    margin: 0;
    padding: 0;
    display: flex;
    flex-direction: column;
    gap: 0.125rem;
}

.session-item {
    display: flex;
    align-items: center;
    gap: 0.25rem;
    padding: 0.375rem 0.5rem;
    border-radius: 6px;
    cursor: pointer;
}

.session-item:hover {
    background: #313244;
}

.session-item--active {
    background: #45475a;
}

.session-name {
    flex: 1;
    font-size: 0.8rem;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.session-rename-input {
    flex: 1;
    font-size: 0.8rem;
    background: #313244;
    border: 1px solid #89b4fa;
    border-radius: 4px;
    color: #cdd6f4;
    padding: 0.125rem 0.25rem;
}

.main-content {
    flex: 1;
    overflow: auto;
    min-width: 0;
}

.ghost-btn--sm {
    font-size: 0.75rem;
    padding: 0.125rem 0.375rem;
}

.ghost-btn--icon {
    font-size: 0.7rem;
    padding: 0.125rem 0.25rem;
    opacity: 0;
}

.session-item:hover .ghost-btn--icon {
    opacity: 1;
}
```

- [ ] **Step 4: Build and run to verify sidebar appears**

```bash
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
dotnet run --project DotnetAiAgentUi/src/HrMcp.Agent -- --web
```
Open `http://localhost:5000`. Verify: sidebar appears on the left, "New Chat" button works, sending a prompt creates a session in the sidebar, navigating to a past session reloads its turns.

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Layout/ \
        DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css
git commit -m "feat(ui): add SessionsSidebar with named sessions, rename, delete, and New Chat"
```

---

### Task 9: Blog Post — Part 7

**Files:**
- Create: `blogs/series-2-ai-agent-ui/part-7-conversation-history.md`

- [ ] **Step 1: Write Part 7 blog post**

Create `blogs/series-2-ai-agent-ui/part-7-conversation-history.md` with:

```markdown
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
```

- [ ] **Step 2: Commit blog post**

```bash
git add blogs/series-2-ai-agent-ui/part-7-conversation-history.md
git commit -m "blog(series-2): add Part 7 — Conversation History"
```
