# Part 8: Login — ASP.NET Core Identity & OIDC Federation

Series: AI Agent UI with Blazor United & .NET 10 | Part 8 of 8
GitHub: workcontrolgit/DotnetAiAgentUiTutorial
![Series 2 cover](screenshots/blog_cover.png)

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
