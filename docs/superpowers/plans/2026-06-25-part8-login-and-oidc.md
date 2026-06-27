# Part 8 — Login & OIDC Federation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add user authentication to the Blazor app using ASP.NET Core Identity (local accounts) with optional external OIDC provider federation, replacing the `"dev-user"` stub with the real authenticated user identity.

**Architecture:** `ApplicationUser : IdentityUser` added to `HrMcp.Core`. `HrDbContext` inherits from `IdentityDbContext<ApplicationUser>`. Identity tables added via EF migration. `Login`, `Register`, and `Logout` Razor pages added to `HrMcp.Agent`. All workspace pages protected with `[Authorize]`. External OIDC federation gated behind `Features:EnableOidc`. `userId` sourced from `AuthenticationStateProvider` via a `UserContext` scoped service.

**Tech Stack:** .NET 10, ASP.NET Core Identity 9, EF Core 9, Blazor United (InteractiveServer), Duende IdentityServer (dev OIDC provider), MudBlazor 8

## Global Constraints

- Target framework: `net10.0` everywhere
- Identity packages: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` Version `9.*`
- `Features:EnableOidc` is the same flag used in Part 6 for MCP client credentials — **do not rename it**
- `userId` must come from `AuthenticationStateProvider`, not `IHttpContextAccessor` (unreliable in Blazor Server)
- All `DevUserId = "dev-user"` constants from Part 7 must be replaced in this plan
- Build command: `dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx`
- Migration command: `dotnet ef migrations add <Name> --project DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence --startup-project DotnetAiAgentUi/src/HrMcp.Agent`

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `HrMcp.Core/Entities/ApplicationUser.cs` | Identity user entity |
| Modify | `HrMcp.Infrastructure.Persistence/HrDbContext.cs` | Inherit `IdentityDbContext<ApplicationUser>` |
| Modify | `HrMcp.Infrastructure.Persistence/DependencyInjection.cs` | Add Identity registration |
| Modify | `HrMcp.Infrastructure.Persistence/HrMcp.Infrastructure.Persistence.csproj` | Add Identity package |
| Create | `HrMcp.Agent/Web/Services/UserContext.cs` | Scoped service resolving `userId` from auth state |
| Modify | `HrMcp.Agent/Web/Services/AgentDraftService.cs` | Remove `"dev-user"` stub, inject `UserContext` |
| Modify | `HrMcp.Agent/Components/Layout/SessionsSidebar.razor` | Inject `UserContext`, remove `DevUserId` |
| Modify | `HrMcp.Agent/Components/Pages/DraftWorkspace.razor` | Add `[Authorize]`, inject `UserContext`, remove `DevUserId` |
| Create | `HrMcp.Agent/Components/Pages/Login.razor` | Login page |
| Create | `HrMcp.Agent/Components/Pages/Register.razor` | Registration page |
| Create | `HrMcp.Agent/Components/Pages/Logout.razor` | Logout endpoint |
| Modify | `HrMcp.Agent/Program.cs` (`RunWebAsync`) | Register Identity, auth middleware, optional OIDC |
| Modify | `HrMcp.Agent/HrMcp.Agent.csproj` | No new packages needed (Identity is in SDK) |
| Modify | `HrMcp.Agent/appsettings.json` | Add OIDC user-login config section |
| Create | (EF migration) | Add Identity tables |
| Create | `blogs/series-2-ai-agent-ui/part-8-login-and-oidc.md` | Blog post |

---

### Task 1: `ApplicationUser` Entity + Identity Package

**Files:**
- Create: `DotnetAiAgentUi/src/HrMcp.Core/Entities/ApplicationUser.cs`
- Modify: `DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/HrMcp.Infrastructure.Persistence.csproj`

**Interfaces:**
- Produces: `ApplicationUser` — consumed by Tasks 2, 3

- [ ] **Step 1: Create `ApplicationUser`**

```csharp
// DotnetAiAgentUi/src/HrMcp.Core/Entities/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

namespace HrMcp.Core.Entities;

public sealed class ApplicationUser : IdentityUser
{
    // Extend with profile properties in future parts
}
```

- [ ] **Step 2: Add Identity EF package to `HrMcp.Infrastructure.Persistence.csproj`**

Add inside an `<ItemGroup>`:

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="9.*" />
```

- [ ] **Step 3: Add Identity package to `HrMcp.Agent.csproj`**

Add inside an `<ItemGroup>`:

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.UI" Version="9.*" />
```

- [ ] **Step 4: Build to confirm**

```bash
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Core/Entities/ApplicationUser.cs \
        DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/HrMcp.Infrastructure.Persistence.csproj \
        DotnetAiAgentUi/src/HrMcp.Agent/HrMcp.Agent.csproj
git commit -m "feat(core): add ApplicationUser entity and Identity packages"
```

---

### Task 2: Update `HrDbContext` to `IdentityDbContext`

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/HrDbContext.cs`

**Interfaces:**
- Consumes: `ApplicationUser` from Task 1
- Produces: `HrDbContext` with Identity tables — consumed by EF migration in Task 4

- [ ] **Step 1: Replace `HrDbContext.cs`**

```csharp
// DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/HrDbContext.cs
using HrMcp.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HrMcp.Infrastructure.Persistence;

public class HrDbContext(DbContextOptions<HrDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<HiringOrganization>   HiringOrganizations   => Set<HiringOrganization>();
    public DbSet<Position>             Positions             => Set<Position>();
    public DbSet<PositionRemuneration> PositionRemunerations => Set<PositionRemuneration>();
    public DbSet<ConversationSession>  ConversationSessions  => Set<ConversationSession>();
    public DbSet<ConversationTurn>     ConversationTurns     => Set<ConversationTurn>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);   // required — sets up Identity tables

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

- [ ] **Step 2: Build to confirm**

```bash
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/HrDbContext.cs
git commit -m "feat(persistence): migrate HrDbContext to IdentityDbContext<ApplicationUser>"
```

---

### Task 3: Register Identity in `DependencyInjection` + `Program.cs`

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/DependencyInjection.cs`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Program.cs`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/appsettings.json`

**Interfaces:**
- Consumes: `ApplicationUser`, `HrDbContext` from Tasks 1–2
- Produces: `UserManager<ApplicationUser>`, `SignInManager<ApplicationUser>`, auth middleware, optional `AddOpenIdConnect`

- [ ] **Step 1: Update `DependencyInjection.cs` to register Identity**

```csharp
// DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/DependencyInjection.cs
using HrMcp.Core.Entities;
using HrMcp.Core.Interfaces;
using HrMcp.Infrastructure.Persistence.Repositories;
using HrMcp.Infrastructure.Persistence.Services;
using Microsoft.AspNetCore.Identity;
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

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<HrDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IHiringOrganizationRepository, HiringOrganizationRepository>();
        services.AddScoped<IConversationService, ConversationService>();

        return services;
    }
}
```

- [ ] **Step 2: Update `RunWebAsync` in `Program.cs` to add auth middleware and optional OIDC**

Replace the `RunWebAsync` function body:

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
    builder.Services.AddScoped<UserContext>();

    // Cookie auth (set by Identity above) + optional OIDC federation
    var enableOidc = builder.Configuration.GetValue<bool>("Features:EnableOidc");
    if (enableOidc)
    {
        builder.Services.AddAuthentication()
            .AddOpenIdConnect("oidc", options =>
            {
                options.Authority = builder.Configuration["Oidc:UserLogin:Authority"]
                    ?? throw new InvalidOperationException("Missing Oidc:UserLogin:Authority");
                options.ClientId = builder.Configuration["Oidc:UserLogin:ClientId"]
                    ?? throw new InvalidOperationException("Missing Oidc:UserLogin:ClientId");
                options.ClientSecret = builder.Configuration["Oidc:UserLogin:ClientSecret"];
                options.ResponseType = "code";
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
            });
    }

    builder.Services.AddAuthorization();

    var app = builder.Build();
    app.UseStaticFiles();
    app.MapStaticAssets();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    Console.WriteLine("HrMcp.Agent starting in --web mode.");
    await app.RunAsync();
}
```

Also add usings at the top of `Program.cs`:

```csharp
using HrMcp.Agent.Web.Services;
using HrMcp.Infrastructure.Persistence;
```

- [ ] **Step 3: Add OIDC user-login config to `appsettings.json`**

Add alongside the existing `Oidc` section (the existing one is for MCP client credentials — this is a separate sub-key for user login):

```json
"Oidc": {
  "Authority": "https://localhost:44310",
  "ClientId": "hr-mcp-agent",
  "ClientSecret": "hr-mcp-agent-secret",
  "Scope": "hr-mcp-api",
  "UserLogin": {
    "Authority": "https://localhost:44310",
    "ClientId": "hr-mcp-web",
    "ClientSecret": "hr-mcp-web-secret"
  }
}
```

- [ ] **Step 4: Build to confirm**

```bash
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/DependencyInjection.cs \
        DotnetAiAgentUi/src/HrMcp.Agent/Program.cs \
        DotnetAiAgentUi/src/HrMcp.Agent/appsettings.json
git commit -m "feat(auth): register Identity, auth middleware, and optional OIDC user-login"
```

---

### Task 4: EF Migration — Identity Tables

**Files:**
- Create: EF migration (auto-generated)

**Interfaces:**
- Consumes: `HrDbContext : IdentityDbContext<ApplicationUser>` from Task 2
- Produces: `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, etc. tables in SQL Server

- [ ] **Step 1: Run migration**

```bash
dotnet ef migrations add AddIdentity \
  --project DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence \
  --startup-project DotnetAiAgentUi/src/HrMcp.Agent
```
Expected: Migration file created.

- [ ] **Step 2: Apply migration**

```bash
dotnet ef database update \
  --project DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence \
  --startup-project DotnetAiAgentUi/src/HrMcp.Agent
```
Expected: `Done. Applied migration 'AddIdentity'.`

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Infrastructure.Persistence/Migrations/
git commit -m "feat(migrations): add AddIdentity migration for ASP.NET Core Identity tables"
```

---

### Task 5: `UserContext` Service

**Files:**
- Create: `DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/UserContext.cs`

**Interfaces:**
- Consumes: `AuthenticationStateProvider` (Blazor built-in)
- Produces: `UserContext.GetUserIdAsync()` — consumed by Tasks 6, 7, 8

- [ ] **Step 1: Create `UserContext`**

```csharp
// DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/UserContext.cs
using Microsoft.AspNetCore.Components.Authorization;

namespace HrMcp.Agent.Web.Services;

public sealed class UserContext(AuthenticationStateProvider authStateProvider)
{
    public async Task<string?> GetUserIdAsync()
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        return state.User.Identity?.IsAuthenticated == true;
    }
}
```

- [ ] **Step 2: Build to confirm**

```bash
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/UserContext.cs
git commit -m "feat(auth): add UserContext service to resolve userId from AuthenticationStateProvider"
```

---

### Task 6: Login, Register, and Logout Pages

**Files:**
- Create: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/Login.razor`
- Create: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/Register.razor`
- Create: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/Logout.razor`

**Interfaces:**
- Consumes: `SignInManager<ApplicationUser>`, `UserManager<ApplicationUser>` from Task 3
- Produces: `/login`, `/register`, `/logout` routes

- [ ] **Step 1: Create `Login.razor`**

```razor
@* DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/Login.razor *@
@page "/login"
@using HrMcp.Core.Entities
@using Microsoft.AspNetCore.Identity
@inject SignInManager<ApplicationUser> SignInManager
@inject NavigationManager Nav

<div class="auth-page">
    <div class="auth-card">
        <h2>Sign In</h2>

        @if (!string.IsNullOrWhiteSpace(_error))
        {
            <div class="auth-error">@_error</div>
        }

        <div class="auth-field">
            <label>Email</label>
            <input type="email" @bind="_email" placeholder="you@example.com" />
        </div>
        <div class="auth-field">
            <label>Password</label>
            <input type="password" @bind="_password" placeholder="Password" />
        </div>
        <button class="primary-btn" @onclick="SignInAsync" disabled="@_busy">Sign In</button>

        <p class="auth-link">No account? <a href="/register">Register</a></p>

        @if (_showOidc)
        {
            <hr />
            <a class="ghost-btn auth-oidc-btn" href="/challenge/oidc">Sign in with Identity Provider</a>
        }
    </div>
</div>

@code {
    [SupplyParameterFromQuery] public string? ReturnUrl { get; set; }

    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _error = string.Empty;
    private bool _busy;
    private bool _showOidc;

    [Inject] private IConfiguration Configuration { get; set; } = default!;

    protected override void OnInitialized()
    {
        _showOidc = Configuration.GetValue<bool>("Features:EnableOidc");
    }

    private async Task SignInAsync()
    {
        _busy = true;
        _error = string.Empty;

        var result = await SignInManager.PasswordSignInAsync(
            _email, _password, isPersistent: false, lockoutOnFailure: false);

        if (result.Succeeded)
            Nav.NavigateTo(ReturnUrl ?? "/", forceLoad: true);
        else
            _error = "Invalid email or password.";

        _busy = false;
    }
}
```

- [ ] **Step 2: Create `Register.razor`**

```razor
@* DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/Register.razor *@
@page "/register"
@using HrMcp.Core.Entities
@using Microsoft.AspNetCore.Identity
@inject UserManager<ApplicationUser> UserManager
@inject SignInManager<ApplicationUser> SignInManager
@inject NavigationManager Nav

<div class="auth-page">
    <div class="auth-card">
        <h2>Create Account</h2>

        @if (!string.IsNullOrWhiteSpace(_error))
        {
            <div class="auth-error">@_error</div>
        }

        <div class="auth-field">
            <label>Email</label>
            <input type="email" @bind="_email" placeholder="you@example.com" />
        </div>
        <div class="auth-field">
            <label>Password</label>
            <input type="password" @bind="_password" placeholder="At least 6 characters" />
        </div>
        <button class="primary-btn" @onclick="RegisterAsync" disabled="@_busy">Create Account</button>
        <p class="auth-link">Already have an account? <a href="/login">Sign in</a></p>
    </div>
</div>

@code {
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _error = string.Empty;
    private bool _busy;

    private async Task RegisterAsync()
    {
        _busy = true;
        _error = string.Empty;

        var user = new ApplicationUser { UserName = _email, Email = _email };
        var result = await UserManager.CreateAsync(user, _password);

        if (result.Succeeded)
        {
            await SignInManager.SignInAsync(user, isPersistent: false);
            Nav.NavigateTo("/", forceLoad: true);
        }
        else
        {
            _error = string.Join(" ", result.Errors.Select(e => e.Description));
        }

        _busy = false;
    }
}
```

- [ ] **Step 3: Create `Logout.razor`**

```razor
@* DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/Logout.razor *@
@page "/logout"
@using HrMcp.Core.Entities
@using Microsoft.AspNetCore.Identity
@inject SignInManager<ApplicationUser> SignInManager
@inject NavigationManager Nav

@code {
    protected override async Task OnInitializedAsync()
    {
        await SignInManager.SignOutAsync();
        Nav.NavigateTo("/login", forceLoad: true);
    }
}
```

- [ ] **Step 4: Add auth page CSS to `app.css`**

Append to `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css`:

```css
/* Auth Pages */
.auth-page {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 100vh;
    background: #1e1e2e;
}

.auth-card {
    background: #24273a;
    border: 1px solid #313244;
    border-radius: 12px;
    padding: 2rem;
    width: 100%;
    max-width: 380px;
    display: flex;
    flex-direction: column;
    gap: 1rem;
    color: #cdd6f4;
}

.auth-card h2 {
    margin: 0;
    font-size: 1.4rem;
    font-weight: 600;
    text-align: center;
}

.auth-field {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
}

.auth-field label {
    font-size: 0.8rem;
    color: #a6adc8;
}

.auth-field input {
    padding: 0.5rem 0.75rem;
    background: #313244;
    border: 1px solid #45475a;
    border-radius: 6px;
    color: #cdd6f4;
    font-size: 0.9rem;
}

.auth-field input:focus {
    outline: none;
    border-color: #89b4fa;
}

.auth-error {
    background: #f38ba826;
    border: 1px solid #f38ba8;
    border-radius: 6px;
    padding: 0.5rem 0.75rem;
    font-size: 0.85rem;
    color: #f38ba8;
}

.auth-link {
    font-size: 0.8rem;
    text-align: center;
    color: #a6adc8;
    margin: 0;
}

.auth-link a {
    color: #89b4fa;
    text-decoration: none;
}

.auth-oidc-btn {
    width: 100%;
    text-align: center;
    justify-content: center;
}
```

- [ ] **Step 5: Build to confirm**

```bash
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/Login.razor \
        DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/Register.razor \
        DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/Logout.razor \
        DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css
git commit -m "feat(auth): add Login, Register, Logout pages"
```

---

### Task 7: Protect Workspace + Replace `DevUserId` Stub

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Pages/DraftWorkspace.razor`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Layout/SessionsSidebar.razor`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/AgentDraftService.cs`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Layout/MainLayout.razor`

**Interfaces:**
- Consumes: `UserContext.GetUserIdAsync()` from Task 5
- Produces: All workspace and sidebar operations scoped to the real authenticated user

- [ ] **Step 1: Add `[Authorize]` and `UserContext` to `DraftWorkspace.razor`**

At the top of `DraftWorkspace.razor`, add `@attribute [Authorize]` and inject `UserContext`:

```razor
@page "/"
@page "/workspace/{SessionId:guid}"
@attribute [Authorize]
@using HrMcp.Agent.Web.Models
@using HrMcp.Agent.Web.Services
@using HrMcp.Core.Interfaces
@using Markdig
@using Microsoft.AspNetCore.Components.Web
@using System.Text
@using System.Text.RegularExpressions
@inject IAgentDraftService AgentDraftService
@inject IConversationService ConversationService
@inject UserContext UserContext
@inject NavigationManager Nav
@inject IJSRuntime JS
```

In the `@code` block, **remove** `private const string DevUserId = "dev-user";` and add a field:

```csharp
private string _userId = string.Empty;
```

Add user ID resolution at the start of `OnParametersSetAsync`:

```csharp
protected override async Task OnParametersSetAsync()
{
    _userId = await UserContext.GetUserIdAsync() ?? string.Empty;

    if (SessionId is null)
    {
        _turns.Clear();
        return;
    }

    var session = await ConversationService.GetSessionAsync(SessionId.Value, _userId);
    // ... rest unchanged
```

Replace every remaining use of `DevUserId` with `_userId`:

- In `SendPromptAsync`: `await ConversationService.CreateSessionAsync(_userId, input)` and `await ConversationService.AddTurnAsync(...)`

- [ ] **Step 2: Update `SessionsSidebar.razor` to use `UserContext`**

Replace the `@code` block's `DevUserId` constant and `LoadSessionsAsync` in `SessionsSidebar.razor`:

Add inject at the top:
```razor
@inject HrMcp.Agent.Web.Services.UserContext UserContext
```

In `@code`, remove `private const string DevUserId = "dev-user";` and add field:

```csharp
private string _userId = string.Empty;
```

Update `OnInitializedAsync`:

```csharp
protected override async Task OnInitializedAsync()
{
    _userId = await UserContext.GetUserIdAsync() ?? string.Empty;
    await LoadSessionsAsync();
    Nav.LocationChanged += OnLocationChanged;
}
```

Replace all `DevUserId` with `_userId` in `LoadSessionsAsync`, `CommitRename`, and `DeleteSession`.

- [ ] **Step 3: Update `AgentDraftService` to use `UserContext` instead of `"dev-user"`**

In `AgentDraftService.cs`, replace the `IConversationService` injection constructor and `SendPromptAsync`:

```csharp
private readonly IConversationService _conversationService;
private readonly UserContext _userContext;

public AgentDraftService(IConversationService conversationService, UserContext userContext)
{
    _conversationService = conversationService;
    _userContext = userContext;
}
```

In `SendPromptAsync`, replace `"dev-user"` with:

```csharp
var userId = await _userContext.GetUserIdAsync() ?? "dev-user";
var session = await _conversationService.GetSessionAsync(sessionId.Value, userId, ct);
```

- [ ] **Step 4: Add `CascadeAuthenticationState` to `App.razor` or configure redirect**

In `DotnetAiAgentUi/src/HrMcp.Agent/Components/App.razor`, wrap `<Routes>` with `CascadingAuthenticationState` and add `AuthorizeRouteView`:

```razor
@using Microsoft.AspNetCore.Components.Authorization

<CascadingAuthenticationState>
    <Router AppAssembly="typeof(App).Assembly">
        <Found Context="routeData">
            <AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)">
                <NotAuthorized>
                    @{
                        Nav.NavigateTo($"/login?returnUrl={Uri.EscapeDataString(Nav.Uri)}", forceLoad: true);
                    }
                </NotAuthorized>
            </AuthorizeRouteView>
        </Found>
        <NotFound>
            <PageTitle>Not found</PageTitle>
            <LayoutView Layout="typeof(Layout.MainLayout)">
                <p role="alert">Sorry, there's nothing at this address.</p>
            </LayoutView>
        </NotFound>
    </Router>
</CascadingAuthenticationState>
```

Also add `@inject NavigationManager Nav` at the top of `App.razor`.

- [ ] **Step 5: Add logout button to `MainLayout.razor` topbar**

In `MainLayout.razor`, add a logout link in the header area:

```razor
@inherits LayoutComponentBase
@using Microsoft.AspNetCore.Components.Authorization

<CascadingAuthenticationState>
    <div class="main-layout">
        <SessionsSidebar />
        <div class="main-content">
            <div class="topbar-auth">
                <AuthorizeView>
                    <Authorized>
                        <span class="topbar-user">@context.User.Identity?.Name</span>
                        <a href="/logout" class="ghost-btn ghost-btn--sm">Sign out</a>
                    </Authorized>
                </AuthorizeView>
            </div>
            @Body
        </div>
    </div>
</CascadingAuthenticationState>
```

Add CSS for `topbar-auth` to `app.css`:

```css
.topbar-auth {
    display: flex;
    align-items: center;
    justify-content: flex-end;
    gap: 0.75rem;
    padding: 0.5rem 1rem;
    border-bottom: 1px solid #313244;
    background: #1e1e2e;
    font-size: 0.8rem;
    color: #a6adc8;
}
.topbar-user {
    color: #cdd6f4;
}
```

- [ ] **Step 6: Build to confirm**

```bash
dotnet build DotnetAiAgentUi/DotnetAiAgentUi.slnx
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Smoke test**

```bash
dotnet run --project DotnetAiAgentUi/src/HrMcp.Agent -- --web
```
Open `http://localhost:5000`. Verify:
1. Unauthenticated visit to `/` redirects to `/login`
2. Register a new account → lands on workspace
3. Send a prompt → session appears in sidebar
4. Sign out → redirected to `/login`
5. Sign back in → sessions list preserved

- [ ] **Step 8: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/ \
        DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/AgentDraftService.cs \
        DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css
git commit -m "feat(auth): protect workspace with [Authorize], replace DevUserId stub with real userId"
```

---

### Task 8: Blog Post — Part 8

**Files:**
- Create: `blogs/series-2-ai-agent-ui/part-8-login-and-oidc.md`

- [ ] **Step 1: Write Part 8 blog post**

```markdown
# Part 8: Login — ASP.NET Core Identity & OIDC Federation

**Series:** [AI Agent UI with Blazor United & .NET 10](preface.md) | **Part 8 of 8**  
**GitHub:** [workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)

---

## Introduction

Part 7 gave the Writing Assistant persistent memory — but every user shares the same `"dev-user"` stub. That was intentional: get the persistence right first, then plug in real authentication. This part does the plug.

By the end you will have:

- ASP.NET Core Identity with local email/password accounts
- Login, Register, and Logout pages
- All workspace routes protected by `[Authorize]`
- Unauthenticated visitors redirected to `/login`
- Conversation sessions scoped to the real authenticated user
- Optional external OIDC federation (Duende IdentityServer in dev, Entra ID / Okta in production) gated behind `Features:EnableOidc`

---

## `ApplicationUser`

```csharp
// src/HrMcp.Core/Entities/ApplicationUser.cs
public sealed class ApplicationUser : IdentityUser { }
```

A sealed class extending `IdentityUser`. Right now it adds nothing — it exists so we can extend it later without a migration. It lives in `HrMcp.Core` because it is a domain entity that other layers depend on.

---

## `IdentityDbContext`

`HrDbContext` changes its base class:

```csharp
public class HrDbContext(DbContextOptions<HrDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    // ... existing DbSets unchanged
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);  // required — sets up Identity tables
        // ... existing configuration unchanged
    }
}
```

The `base.OnModelCreating` call is not optional. Without it, the Identity tables are not mapped and the migration will produce no Identity schema.

---

## Registering Identity

In `DependencyInjection.AddPersistence`:

```csharp
services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<HrDbContext>()
    .AddDefaultTokenProviders();
```

Password rules are relaxed for development ergonomics — tighten them for production.

---

## `UserContext` Service

`IHttpContextAccessor` is unreliable in Blazor Server because the HTTP context is not available after the initial render. The correct approach is `AuthenticationStateProvider`:

```csharp
// src/HrMcp.Agent/Web/Services/UserContext.cs
public sealed class UserContext(AuthenticationStateProvider authStateProvider)
{
    public async Task<string?> GetUserIdAsync()
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
```

`UserContext` is registered as `AddScoped<UserContext>()` and injected anywhere `userId` is needed — `DraftWorkspace`, `SessionsSidebar`, `AgentDraftService`.

---

## Login Page

`/login` uses `SignInManager.PasswordSignInAsync`. On success it navigates with `forceLoad: true` to flush the auth state cookie:

```csharp
var result = await SignInManager.PasswordSignInAsync(
    _email, _password, isPersistent: false, lockoutOnFailure: false);

if (result.Succeeded)
    Nav.NavigateTo(ReturnUrl ?? "/", forceLoad: true);
else
    _error = "Invalid email or password.";
```

When `Features:EnableOidc` is true, a "Sign in with Identity Provider" button appears — it links to `/challenge/oidc` which ASP.NET Core handles automatically via the `AddOpenIdConnect` registration.

---

## Optional OIDC Federation

The OIDC user-login flow is separate from Part 6's client credentials flow. Client credentials is machine-to-machine (the agent acquiring a token to call the MCP server). User login is human-to-machine (a browser user authenticating to the Blazor app):

```csharp
// appsettings.json
"Oidc": {
  "Authority": "...",          // Part 6: MCP server client credentials
  "ClientId": "hr-mcp-agent",
  "UserLogin": {               // Part 8: user-facing browser login
    "Authority": "https://localhost:44310",
    "ClientId": "hr-mcp-web"
  }
}
```

In `Program.cs`:

```csharp
if (enableOidc)
{
    builder.Services.AddAuthentication()
        .AddOpenIdConnect("oidc", options =>
        {
            options.Authority = configuration["Oidc:UserLogin:Authority"];
            options.ClientId = configuration["Oidc:UserLogin:ClientId"];
            options.ResponseType = "code";
            options.SaveTokens = true;
        });
}
```

To swap providers in production, change the two `Oidc:UserLogin` values — nothing else changes.

---

## Protecting Routes

`DraftWorkspace.razor` gains `@attribute [Authorize]`. `App.razor` wraps the router in `CascadingAuthenticationState` and uses `AuthorizeRouteView`. Unauthenticated visits trigger a `NavigateTo("/login?returnUrl=...")` redirect.

---

## Running It

```bash
dotnet run --project DotnetAiAgentUi/src/HrMcp.Agent -- --web
```

1. Visit `http://localhost:5000` — redirected to `/login`
2. Click Register, create an account
3. Land on the workspace — sidebar shows your sessions
4. Sign out, sign back in — history is preserved

---

## What We Built

- `ApplicationUser : IdentityUser` in `HrMcp.Core`
- `HrDbContext : IdentityDbContext<ApplicationUser>` with `base.OnModelCreating()`
- ASP.NET Core Identity with relaxed dev password rules
- EF migration `AddIdentity`
- `UserContext` scoped service using `AuthenticationStateProvider`
- Login, Register, Logout pages
- `[Authorize]` on `DraftWorkspace` with redirect to `/login`
- `DevUserId` stub replaced everywhere with real authenticated user ID
- Optional OIDC federation via `Features:EnableOidc`

---

Tags: `dotnet` `blazor` `csharp` `aspnetcoreidentity` `oidc` `authentication`
```

- [ ] **Step 2: Commit blog post**

```bash
git add blogs/series-2-ai-agent-ui/part-8-login-and-oidc.md
git commit -m "blog(series-2): add Part 8 — Login & OIDC Federation"
```
