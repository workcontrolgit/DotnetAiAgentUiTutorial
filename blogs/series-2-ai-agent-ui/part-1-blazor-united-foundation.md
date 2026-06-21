# Part 1: Blazor United Foundation

**Series:** [AI Agent UI with Blazor United & .NET 10](preface.md) | **Part 1 of 6**
**GitHub:** [workcontrolgit/DotnetAiAgentUiTutorial](https://github.com/workcontrolgit/DotnetAiAgentUiTutorial)
![Series 2 cover](screenshots/blog-cover.png)

---

## Introduction

In Series 1 we built the backend: a Clean Architecture .NET 10 solution, an MCP server with HR domain tools, and a console AI agent that queries it. That series started with the domain foundation — the thing every other part depended on. This series does the same thing for the frontend.

Part 1 here is the exact parallel of Series 1 Part 1. We are not wiring up the AI yet. We are not connecting to the MCP server yet. We are building the shell that everything else will run inside — a Blazor United application with a MudBlazor layout, proper routing, and a dual-mode startup that lets the same binary run as either a console agent or a web UI.

By the end of this post you will have the project created, MudBlazor installed, a working layout, and a browser window showing the navigation shell. No AI, no chat, no MCP. Just a clean Blazor United app you can extend across the next five parts.

The `--web` flag that launches the web UI is already in `Program.cs`. We will look at exactly what it does and why I structured startup that way.

---

## Why Blazor United?

Blazor United — the official Blazor template with `--interactivity Auto` — gives you Auto render mode. This is not the same as Blazor Server or Blazor WASM. It is a combination of both.

Here is the difference in plain terms:

- **Blazor Server (pre-United):** Every component runs on the server. Every keystroke is a SignalR round-trip. Works instantly, but you own the connection.
- **Blazor WASM:** Every component runs in the browser. First load is slow (runtime download), but after that it is entirely client-side.
- **Blazor United with Auto render mode:** Components start with server-side prerendering (SSR) for fast initial load, then interactivity is handled by the server via SignalR. If the WASM runtime is available in the browser cache, future visits can switch to WASM. You get the best of both without writing two apps.

For an AI Agent UI, this matters. We need real-time streaming of AI responses — tokens arriving one at a time, chat state updated incrementally, no page reloads. That requires interactive components. But we also want the initial page load to feel fast, not blank while JavaScript initializes. Blazor United gives us both.

.NET 10 is the target framework throughout this series. It ships with refinements to the Blazor United model, including improved static asset handling through `MapStaticAssets` and better tooling support for the `blazor` template.

There is one more reason I chose Blazor United over a separate React or Angular frontend: the whole point of this tutorial series is to show what a .NET developer can build without leaving the .NET ecosystem. Blazor United keeps everything in C#, on the same project, with shared types — no REST API surface to maintain between the UI and the agent logic.

---

## Step 1 — Create the Blazor United Project

The `blazor` template (not `blazorserver` or `blazorwasm`) is what creates a Blazor United app:

```bash
dotnet new blazor -n HrMcp.Agent --interactivity Auto --all-interactive
```

The `--interactivity Auto` flag sets the default render mode to Auto. The `--all-interactive` flag applies that render mode globally at the app level rather than requiring you to annotate each page individually.

The resulting project file uses `Microsoft.NET.Sdk.Web` as the SDK and `OutputType = Exe` so the project can be run directly as an executable. That `OutputType = Exe` is important: it is what allows the same binary to behave as a console application when launched without `--web`, and as an ASP.NET Core web host when launched with `--web`.

Here is the actual project file for `HrMcp.Agent`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.AI" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="10.*" />
    <PackageReference Include="Azure.AI.OpenAI" Version="2.*" />
    <PackageReference Include="Azure.Identity" Version="1.*" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="10.*" />
    <PackageReference Include="OllamaSharp" Version="5.*" />
    <PackageReference Include="ModelContextProtocol" Version="1.*" />
    <PackageReference Include="Spectre.Console" Version="0.49.*" />
    <PackageReference Include="Markdig" Version="0.40.*" />
    <PackageReference Include="Serilog" Version="4.*" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.*" />
    <PackageReference Include="MudBlazor" Version="8.*" />
    <PackageReference Include="Blazored.TextEditor" Version="1.*" />
  </ItemGroup>

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UserSecretsId>DotnetAiAgentMcp-HrMcp-Agent</UserSecretsId>
  </PropertyGroup>

</Project>
```

Notice `Microsoft.NET.Sdk.Web` as the SDK — this is what enables Razor components, static web assets, and ASP.NET Core middleware. `OutputType = Exe` gives us the dual-mode startup. Target framework is `net10.0`.

---

## Step 2 — Install MudBlazor 8

MudBlazor is my choice for the component library in this series. I have used it on real projects and on tutorials, and it consistently hits the right balance: rich enough to build a professional UI without fighting the framework, conservative enough that it does not produce opinionated visual output that fights with your own layout decisions.

Version 8 is the current major that targets .NET 8 and above, including .NET 10.

```bash
dotnet add DotnetAiAgentUI/src/HrMcp.Agent package MudBlazor --version 8.*
```

Once the package is installed, three setup steps wire it into the application.

**Step 2a — Add the namespace to `_Imports.razor`**

The `_Imports.razor` file in the project adds the `@using MudBlazor` directive so every component in the project can use MudBlazor types without per-file imports:

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.JSInterop
@using MudBlazor
@using Blazored.TextEditor
@using HrMcp.Agent
```

**Step 2b — Add CSS/JS links and providers to `App.razor`**

MudBlazor requires its CSS in the `<head>` and its providers in the `<body>`. The actual `App.razor` in this project uses `HeadOutlet` with `RenderMode.InteractiveServer` and `Routes` with `RenderMode.InteractiveServer`, which is the correct pattern for Blazor United when you want fully interactive rendering across the app.

The MudBlazor providers — `MudThemeProvider`, `MudDialogProvider`, `MudSnackbarProvider` — are typically added to your root layout rather than `App.razor` directly, so that they participate in the component tree.

**Step 2c — Why MudBlazor over the alternatives**

Fluent UI Blazor (Microsoft's component library) is the other serious contender. I chose MudBlazor for this tutorial because:

- The API surface is more ergonomic for tutorial code — less ceremony to get a working UI fragment
- The documentation is excellent for .NET developers coming from Material Design backgrounds
- MudBlazor 8 has first-class support for Blazor United render modes
- The component set (dialogs, drawers, app bars, data grids) maps directly to what an AI Agent UI needs

If your production project is already on Fluent UI Blazor, the concepts in this series transfer directly — the component names change but the patterns do not.

---

## Step 3 — Configure the Main Layout

The layout in this project is in `DotnetAiAgentUI/src/HrMcp.Agent/Components/Layout/MainLayout.razor`. The current file is intentionally minimal — a wrapper `div` that hosts `@Body`:

```razor
@inherits LayoutComponentBase

<div class="main-layout">
    @Body
</div>
```

This is the foundation. The layout inherits from `LayoutComponentBase`, which is the Blazor contract for layout components. `@Body` renders whatever page is currently routed to.

In a full MudBlazor layout you would expand this to include `MudLayout`, `MudAppBar`, `MudDrawer`, and `MudMainContent`. The minimal shell here is deliberate: we want the app to boot and confirm that the layout pipeline is wired up before we add navigation chrome. Here is what an expanded MudBlazor shell looks like:

```razor
@inherits LayoutComponentBase

<MudThemeProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="1">
        <MudIconButton Icon="@Icons.Material.Filled.Menu"
                       Color="Color.Inherit"
                       Edge="Edge.Start"
                       OnClick="@ToggleDrawer" />
        <MudText Typo="Typo.h6" Class="ml-3">HR Agent</MudText>
        <MudSpacer />
    </MudAppBar>

    <MudDrawer @bind-Open="_drawerOpen" ClipMode="DrawerClipMode.Always" Elevation="2">
        <NavMenu />
    </MudDrawer>

    <MudMainContent>
        <MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
            @Body
        </MudContainer>
    </MudMainContent>
</MudLayout>

@code {
    private bool _drawerOpen = true;

    private void ToggleDrawer() => _drawerOpen = !_drawerOpen;
}
```

Each MudBlazor layout component has a specific role:

- `MudLayout` — the outermost container that coordinates the app bar, drawer, and main content areas
- `MudAppBar` — the top navigation bar; `Elevation` controls the Material Design shadow
- `MudDrawer` — the side navigation panel; `@bind-Open` binds the open/close state bidirectionally
- `MudMainContent` — the content area that adjusts its left margin when the drawer is open or closed
- `MudContainer` — a centered, max-width content wrapper inside the main area

The three provider components (`MudThemeProvider`, `MudDialogProvider`, `MudSnackbarProvider`) sit at the top of the layout tree. They do not render visible UI — they are service components that manage theming, modal dialogs, and toast notifications across the entire app. They need to be in the component hierarchy before any component tries to use dialogs or snackbars.

---

## Step 4 — Set Up Routing

Routing in Blazor United is handled by two files working together: `App.razor` and `Routes.razor`.

`App.razor` is the HTML shell — the file that produces the actual HTML document sent to the browser. The actual `App.razor` in this project:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <link rel="stylesheet" href="css/app.css" />
    <HeadOutlet @rendermode="RenderMode.InteractiveServer" />
</head>
<body>
    <Routes @rendermode="RenderMode.InteractiveServer" />
    <script src="_framework/blazor.web.js"></script>
</body>
</html>
```

Two things to notice here. First, `HeadOutlet` uses `@rendermode="RenderMode.InteractiveServer"`. This is the Blazor United pattern for applying a render mode at the root level — all descendant components inherit this render mode unless they override it. Second, `Routes` also carries `@rendermode="RenderMode.InteractiveServer"`, which means the entire routing tree runs in interactive server mode.

`Routes.razor` is the component that actually performs routing. The actual file in this project:

```razor
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
    <NotFound>
        <LayoutView Layout="typeof(Layout.MainLayout)">
            <p>Sorry, nothing exists at this address.</p>
        </LayoutView>
    </NotFound>
</Router>
```

`Router` scans `typeof(Program).Assembly` for components with `@page` directives. When a route matches, `RouteView` renders the matched component using `MainLayout` as the default layout. `FocusOnNavigate` moves keyboard focus to the `h1` element after navigation — a small but important accessibility feature that Blazor United includes by default. The `NotFound` slot handles routes that do not match, also wrapped in `MainLayout` for visual consistency.

This is different from Blazor Server's routing setup in one important way: the render mode is declared at the `App.razor` level rather than on individual pages. In Blazor United, you choose between per-page render mode declarations and a global declaration. We are using the global approach here because every page in this app needs to be interactive — there are no static-only pages.

---

## Step 5 — Run the Shell

The project uses a dual-mode startup pattern. Without any flags, `dotnet run` launches the console AI agent. With `--web`, it launches the Blazor web UI.

```bash
dotnet run --project DotnetAiAgentUI/src/HrMcp.Agent -- --web
```

The `--` separator tells `dotnet run` that everything after it is arguments for the application itself, not for the `dotnet` CLI. The `--web` flag is consumed by the very first lines of `Program.cs`:

```csharp
var runWeb = args.Contains("--web", StringComparer.OrdinalIgnoreCase);
if (runWeb)
{
    await RunWebAsync(args);
    return;
}
```

If `--web` is present, execution jumps immediately to `RunWebAsync` and returns. The entire console agent setup — MCP client creation, tool discovery, Spectre.Console menus, the agent loop — is bypassed. This is a deliberate design choice: the two modes share the same binary and the same configuration file, but they are entirely independent execution paths.

Here is the full `RunWebAsync` method:

```csharp
static async Task RunWebAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.UseStaticWebAssets();
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
    builder.Services.AddScoped<IAgentDraftService, AgentDraftService>();

    var app = builder.Build();
    app.UseStaticFiles();
    app.MapStaticAssets();
    app.UseAntiforgery();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    Console.WriteLine("HrMcp.Agent starting in --web mode.");
    await app.RunAsync();
}
```

Line by line:

- `WebApplication.CreateBuilder(args)` — standard ASP.NET Core host builder
- `builder.WebHost.UseStaticWebAssets()` — enables the Blazor static web asset pipeline, which is how MudBlazor's bundled CSS and fonts are served in development
- `AddRazorComponents().AddInteractiveServerComponents()` — registers Razor component rendering and the SignalR-backed interactive server renderer
- `AddScoped<IAgentDraftService, AgentDraftService>()` — registers the service that the chat UI components use to manage draft state; it is scoped to the SignalR circuit, which is the correct lifetime for Blazor Server
- `app.UseStaticFiles()` and `app.MapStaticAssets()` — both are present; `MapStaticAssets` is the .NET 10 optimized static file handler that handles fingerprinted asset URLs generated by the Blazor build pipeline
- `app.UseAntiforgery()` — required by Blazor United to protect form endpoints
- `app.MapRazorComponents<App>().AddInteractiveServerRenderMode()` — maps the component hierarchy starting at `App` and enables the interactive server render mode on the endpoint

When you run with `--web` and open the browser, you should see the MudBlazor layout shell: the app bar at the top, the navigation drawer on the left, and the main content area ready for pages. There is no AI, no chat panel, no MCP connection at this stage. That is correct. This is the foundation.

---

## What We Have

At this point the project builds, starts, and shows a working layout in the browser.

- A Blazor United app using `Microsoft.NET.Sdk.Web` and `OutputType = Exe`
- `net10.0` as the target framework
- MudBlazor 8 installed and the namespace imported via `_Imports.razor`
- A `MainLayout.razor` that wraps `@Body` and can be expanded with MudBlazor navigation components
- `App.razor` with `HeadOutlet` and `Routes` both set to `RenderMode.InteractiveServer`
- `Routes.razor` with a `Router` scanning the assembly for `@page` components
- A `RunWebAsync` method in `Program.cs` that branches on the `--web` argument and starts the ASP.NET Core host

This is the same milestone as Series 1 Part 1: a clean, stable foundation that every other part builds on. The AI still knows nothing about this UI. We have not connected to the MCP server. We have not written a single chat component. That is exactly right.

The value of this foundation step is that we now have a single binary that can run as a console agent (for automation, scripting, CI pipelines) or as a web UI (for interactive use). The same configuration file, the same AI provider settings, the same MCP server address — one project, two modes.

---

## Next Up

Part 2 covers the concepts behind AI Agent UI design — `IChatClient`, component state, and the provider-swap pattern — before we write a single chat component.

→ **[Part 2: AI Agent UI Patterns](part-2-ai-agent-ui-patterns.md)**

---

*Tags: .NET, Blazor, AI, MudBlazor, Agent UI, MCP*
