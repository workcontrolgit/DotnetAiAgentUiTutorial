# Theme Switcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Light / Dark / Sepia theme switcher to the Settings modal, persisted via `localStorage`, defaulting to Light.

**Architecture:** A `data-theme` attribute on `<html>` drives CSS variable blocks in `app.css`. A minimal `theme.js` handles `localStorage` read/write and attribute setting. `ThemeService` (scoped Blazor service) holds the current theme in memory and calls JS interop. The Settings modal exposes a button group to switch themes.

**Tech Stack:** Blazor Server (.NET 10), CSS custom properties, vanilla JS, xUnit

## Global Constraints

- Target framework: `net10.0`
- Default theme: `"light"` (Catppuccin Latte)
- `localStorage` key: `hr-theme`
- Theme values: `"light"`, `"dark"`, `"sepia"` (lowercase strings)
- No page reload on theme switch
- All CSS uses existing token variable names — no new variable names introduced

---

### Task 1: CSS Theme Tokens + Button Group Styles

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css`

**Interfaces:**
- Produces: `data-theme="light|dark|sepia"` attribute on `<html>` drives all colors; `.theme-btn` and `.theme-btn--active` classes for the Settings modal button group

**What to do:** Replace the existing `:root { }` block (lines 1–16, the Catppuccin Mocha tokens) with three theme blocks. Add `.theme-btn-group` and `.theme-btn` classes at the end of the file.

- [ ] **Step 1: Replace the `:root` token block**

Open `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css`.

Replace lines 1–16 (the existing `:root { ... }` design token block) with:

```css
/* ── Design tokens ─────────────────────────────────────────────────────────── */

/* Light theme (Catppuccin Latte) — default, applied before JS loads */
:root,
:root[data-theme="light"] {
    --base:      #eff1f5;
    --mantle:    #e6e9ef;
    --crust:     #dce0e8;
    --surface0:  #ccd0da;
    --surface1:  #bcc0cc;
    --surface2:  #acb0be;
    --overlay1:  #8c8fa1;
    --subtext0:  #6c6f85;
    --text:      #4c4f69;
    --blue:      #1e66f5;
    --blue-dark: #1a5ce0;
    --red:       #d20f39;
    --border:    #ccd0da;
}

/* Dark theme (Catppuccin Mocha) */
:root[data-theme="dark"] {
    --base:      #1e1e2e;
    --mantle:    #181825;
    --crust:     #11111b;
    --surface0:  #313244;
    --surface1:  #45475a;
    --surface2:  #585b70;
    --overlay1:  #7f849c;
    --subtext0:  #a6adc8;
    --text:      #cdd6f4;
    --blue:      #89b4fa;
    --blue-dark: #6495f4;
    --red:       #f38ba8;
    --border:    #313244;
}

/* Sepia theme */
:root[data-theme="sepia"] {
    --base:      #f4f0e8;
    --mantle:    #ede8dc;
    --crust:     #e3ddd0;
    --surface0:  #d5cfc2;
    --surface1:  #c8c2b4;
    --surface2:  #b8b2a4;
    --overlay1:  #8a8070;
    --subtext0:  #6a6055;
    --text:      #3c3228;
    --blue:      #7c5c3a;
    --blue-dark: #6b4e30;
    --red:       #a0402a;
    --border:    #d5cfc2;
}
```

- [ ] **Step 2: Add theme button group styles**

Append to the end of `app.css` (after the `@media` block):

```css
/* ── Theme switcher button group ──────────────────────────────────────────── */

.theme-btn-group {
    display: flex;
    gap: 6px;
}

.theme-btn {
    border: 1px solid var(--surface1);
    background: var(--surface0);
    color: var(--text);
    border-radius: 6px;
    padding: 5px 10px;
    cursor: pointer;
    font-size: 0.8rem;
}

.theme-btn--active {
    border-color: var(--blue);
    background: var(--surface1);
    color: var(--text);
    font-weight: 600;
}

.theme-btn:hover:not(.theme-btn--active) {
    background: var(--surface1);
}
```

- [ ] **Step 3: Verify visually**

Run the app (`dotnet run` from `DotnetAiAgentUi/src/HrMcp.Agent/`). The app should display with a **white/light background and dark text** (Catppuccin Latte) — no dark background visible.

- [ ] **Step 4: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/css/app.css
git commit -m "feat(theme): add light/dark/sepia CSS token blocks and theme button styles"
```

---

### Task 2: theme.js + Script Tag

**Files:**
- Create: `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/js/theme.js`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/App.razor`

**Interfaces:**
- Produces: `window.theme.get()` → `string` (current theme name from localStorage, default `"light"`); `window.theme.set(name: string)` → `void` (writes to localStorage + sets `data-theme` on `<html>`)
- These are called by `ThemeService` in Task 3.

- [ ] **Step 1: Create `wwwroot/js/` directory and `theme.js`**

Create `DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/js/theme.js`:

```js
window.theme = {
    get: function () {
        return localStorage.getItem('hr-theme') ?? 'light';
    },
    set: function (name) {
        localStorage.setItem('hr-theme', name);
        document.documentElement.setAttribute('data-theme', name);
    }
};
```

- [ ] **Step 2: Add script tag to `App.razor`**

In `DotnetAiAgentUi/src/HrMcp.Agent/Components/App.razor`, add `<script src="js/theme.js"></script>` immediately before the `<script src="_framework/blazor.web.js">` line:

```html
    <script src="js/theme.js"></script>
    <script src="_framework/blazor.web.js"></script>
```

- [ ] **Step 3: Verify script loads**

Run the app and open browser DevTools → Console. Type `theme.get()` — it should return `"light"`. Type `theme.set("dark")` — background should switch to dark instantly. Refresh — background should remain dark (persisted to localStorage). Type `theme.set("light")` to reset.

- [ ] **Step 4: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/wwwroot/js/theme.js
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/App.razor
git commit -m "feat(theme): add theme.js localStorage/data-attribute helper"
```

---

### Task 3: ThemeService + Unit Tests

**Files:**
- Create: `DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/ThemeService.cs`
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Program.cs`
- Modify: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj`
- Create: `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ThemeServiceTests.cs`

**Interfaces:**
- Produces:
  - `ThemeService.Theme` → `string` (current theme, initial value `"light"`)
  - `ThemeService.InitAsync(IJSRuntime js)` → `Task` (reads `localStorage` via `theme.get`, applies it, updates `Theme`)
  - `ThemeService.SetThemeAsync(IJSRuntime js, string name)` → `Task` (writes to `localStorage` via `theme.set`, updates `Theme`)
- Consumed by: `MainLayout.razor` (Task 4) and `SettingsModal.razor` (Task 5)

- [ ] **Step 1: Write the failing tests**

Add `Microsoft.AspNetCore.Components` to the test project so `IJSRuntime` is available. Update `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\HrMcp.Infrastructure.Persistence\HrMcp.Infrastructure.Persistence.csproj" />
    <ProjectReference Include="..\..\src\HrMcp.Agent\HrMcp.Agent.csproj" />
  </ItemGroup>

</Project>
```

Create `DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ThemeServiceTests.cs`:

```csharp
using HrMcp.Agent.Web.Services;
using Microsoft.JSInterop;

namespace HrMcp.Agent.Tests;

public sealed class ThemeServiceTests
{
    [Fact]
    public void Theme_DefaultsToLight()
    {
        var svc = new ThemeService();
        Assert.Equal("light", svc.Theme);
    }

    [Fact]
    public async Task SetThemeAsync_UpdatesThemeProperty()
    {
        var svc = new ThemeService();
        var js = new FakeJsRuntime("light");

        await svc.SetThemeAsync(js, "dark");

        Assert.Equal("dark", svc.Theme);
    }

    [Fact]
    public async Task SetThemeAsync_CallsThemeSet_WithCorrectName()
    {
        var svc = new ThemeService();
        var js = new FakeJsRuntime("light");

        await svc.SetThemeAsync(js, "sepia");

        Assert.Equal("sepia", js.LastSetName);
    }

    [Fact]
    public async Task InitAsync_SetsThemeFromStorage()
    {
        var svc = new ThemeService();
        var js = new FakeJsRuntime(storedTheme: "dark");

        await svc.InitAsync(js);

        Assert.Equal("dark", svc.Theme);
    }

    // Minimal fake — records the last name passed to theme.set and returns a fixed stored theme
    private sealed class FakeJsRuntime(string storedTheme) : IJSRuntime
    {
        public string? LastSetName { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args,
            CancellationToken cancellationToken = default)
        {
            // theme.get() returns the stored theme
            if (identifier == "theme.get")
                return ValueTask.FromResult((TValue)(object)storedTheme);

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken,
            object?[]? args)
            => InvokeAsync<TValue>(identifier, args, cancellationToken);

        // theme.set(name) — capture the name argument
        ValueTask IJSRuntime.InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new NotImplementedException();

        public ValueTask InvokeVoidAsync(string identifier, params object?[]? args)
        {
            if (identifier == "theme.set" && args?.Length > 0)
                LastSetName = args[0]?.ToString();
            return ValueTask.CompletedTask;
        }
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
cd DotnetAiAgentUi/tests/HrMcp.Agent.Tests
dotnet test --filter "FullyQualifiedName~ThemeServiceTests" -v minimal
```

Expected: compile error — `ThemeService` not found yet.

- [ ] **Step 3: Create `ThemeService.cs`**

Create `DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/ThemeService.cs`:

```csharp
using Microsoft.JSInterop;

namespace HrMcp.Agent.Web.Services;

public sealed class ThemeService
{
    public string Theme { get; private set; } = "light";

    public async Task InitAsync(IJSRuntime js)
    {
        Theme = await js.InvokeAsync<string>("theme.get");
        await js.InvokeVoidAsync("theme.set", Theme);
    }

    public async Task SetThemeAsync(IJSRuntime js, string name)
    {
        Theme = name;
        await js.InvokeVoidAsync("theme.set", name);
    }
}
```

- [ ] **Step 4: Fix the `FakeJsRuntime` to satisfy `IJSRuntime` interface**

The `IJSRuntime` interface has only one method: `InvokeAsync<TValue>(string identifier, CancellationToken, object?[]?)`. Update `FakeJsRuntime` in the test file to implement the interface correctly:

```csharp
private sealed class FakeJsRuntime(string storedTheme) : IJSRuntime
{
    public string? LastSetName { get; private set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (identifier == "theme.get")
            return ValueTask.FromResult((TValue)(object)storedTheme);

        if (identifier == "theme.set" && args?.Length > 0)
            LastSetName = args[0]?.ToString();

        return ValueTask.FromResult(default(TValue)!);
    }
}
```

> Note: `IJSRuntime` in .NET 10 has a single abstract method. `InvokeVoidAsync` is an extension method that calls `InvokeAsync<IJSVoidResult>`. The fake only needs to implement `InvokeAsync<TValue>(string, CancellationToken, object?[]?)`.

- [ ] **Step 5: Run tests — verify they pass**

```bash
dotnet test --filter "FullyQualifiedName~ThemeServiceTests" -v minimal
```

Expected output:
```
Passed  ThemeServiceTests.Theme_DefaultsToLight
Passed  ThemeServiceTests.SetThemeAsync_UpdatesThemeProperty
Passed  ThemeServiceTests.SetThemeAsync_CallsThemeSet_WithCorrectName
Passed  ThemeServiceTests.InitAsync_SetsThemeFromStorage
```

- [ ] **Step 6: Register `ThemeService` in `Program.cs`**

In `DotnetAiAgentUi/src/HrMcp.Agent/Program.cs`, find the service registration block (near line 216 where `AgentDraftService` is registered) and add:

```csharp
builder.Services.AddScoped<ThemeService>();
```

Place it alongside the other scoped registrations:

```csharp
builder.Services.AddScoped<IAgentDraftService, AgentDraftService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<UserContext>();
```

- [ ] **Step 7: Build to verify**

```bash
cd DotnetAiAgentUi/src/HrMcp.Agent
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Web/Services/ThemeService.cs
git add DotnetAiAgentUi/src/HrMcp.Agent/Program.cs
git add DotnetAiAgentUi/tests/HrMcp.Agent.Tests/HrMcp.Agent.Tests.csproj
git add DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ThemeServiceTests.cs
git commit -m "feat(theme): add ThemeService with unit tests and register in DI"
```

---

### Task 4: MainLayout — Apply Theme on Load

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Layout/MainLayout.razor`

**Interfaces:**
- Consumes: `ThemeService.InitAsync(IJSRuntime)` from Task 3
- Produces: Theme applied to `<html>` on every page load before the user sees content

- [ ] **Step 1: Update `MainLayout.razor`**

The current file has no code block. Replace the entire file content with:

```razor
@inherits LayoutComponentBase
@inject ThemeService ThemeService
@inject IJSRuntime JS

<div class="main-layout">
    <SessionsSidebar @rendermode="RenderMode.InteractiveServer" />
    <div class="main-content">
        @Body
    </div>
</div>

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await ThemeService.InitAsync(JS);
    }
}
```

- [ ] **Step 2: Verify**

Run the app. Set theme to `"dark"` in DevTools console (`theme.set("dark")`), then refresh. The page should load dark immediately (not flash light first, then switch).

- [ ] **Step 3: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Layout/MainLayout.razor
git commit -m "feat(theme): initialize theme from localStorage in MainLayout"
```

---

### Task 5: Settings Modal — Theme Button Group

**Files:**
- Modify: `DotnetAiAgentUi/src/HrMcp.Agent/Components/Layout/SettingsModal.razor`

**Interfaces:**
- Consumes: `ThemeService.Theme` (string, current theme) and `ThemeService.SetThemeAsync(IJSRuntime, string)` from Task 3
- Produces: Three theme buttons in the Settings modal; clicking one switches theme instantly

- [ ] **Step 1: Update `SettingsModal.razor`**

Replace the entire file content with:

```razor
@inject IConfiguration Config
@inject ThemeService ThemeService
@inject IJSRuntime JS

<button class="icon-btn" @onclick="Open" title="Settings">&#9881;</button>

@if (_open)
{
    <div class="modal-overlay" @onclick="Close">
        <div class="modal-card" @onclick:stopPropagation="true">
            <div class="modal-header">
                <h2>Settings</h2>
                <button class="modal-close-btn" @onclick="Close" title="Close">&times;</button>
            </div>
            <table class="settings-table">
                <tbody>
                    <tr>
                        <td>Theme</td>
                        <td>
                            <div class="theme-btn-group">
                                <button class="theme-btn @(ThemeService.Theme == "light" ? "theme-btn--active" : "")"
                                        @onclick='() => SetThemeAsync("light")'>Light</button>
                                <button class="theme-btn @(ThemeService.Theme == "dark" ? "theme-btn--active" : "")"
                                        @onclick='() => SetThemeAsync("dark")'>Dark</button>
                                <button class="theme-btn @(ThemeService.Theme == "sepia" ? "theme-btn--active" : "")"
                                        @onclick='() => SetThemeAsync("sepia")'>Sepia</button>
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td>AI Provider</td>
                        <td>@(Config["AI:Provider"] ?? "—")</td>
                    </tr>
                    <tr>
                        <td>Model</td>
                        <td>@ResolveModel()</td>
                    </tr>
                    <tr>
                        <td>Transport</td>
                        <td>@(Config["McpServer:Transport:Type"] ?? "—")</td>
                    </tr>
                    <tr>
                        <td>App Version</td>
                        <td>1.0</td>
                    </tr>
                </tbody>
            </table>
        </div>
    </div>
}

@code {
    private bool _open;

    private void Open() => _open = true;
    private void Close() => _open = false;

    private async Task SetThemeAsync(string name)
    {
        await ThemeService.SetThemeAsync(JS, name);
        StateHasChanged();
    }

    private string ResolveModel()
    {
        var provider = Config["AI:Provider"] ?? "Ollama";
        return string.Equals(provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase)
            ? Config["AI:AzureOpenAI:Deployment"] ?? "—"
            : Config["AI:Ollama:Model"] ?? "—";
    }
}
```

> `StateHasChanged()` is called after `SetThemeAsync` so the active button highlight updates immediately within the modal.

- [ ] **Step 2: Build**

```bash
cd DotnetAiAgentUi/src/HrMcp.Agent
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Run all tests**

```bash
cd DotnetAiAgentUi/tests/HrMcp.Agent.Tests
dotnet test -v minimal
```

Expected: All tests pass.

- [ ] **Step 4: Verify end-to-end**

Run the app. Open the Settings modal (gear icon in the sidebar footer).

Verify:
- `[ Light ]  [ Dark ]  [ Sepia ]` buttons appear in the Theme row
- **Light** is highlighted (blue border) by default
- Clicking **Dark** switches the entire app to dark theme instantly
- Clicking **Sepia** switches to sepia
- Closing the modal and refreshing the page retains the selected theme
- Reopening Settings shows the correct button highlighted

- [ ] **Step 5: Commit**

```bash
git add DotnetAiAgentUi/src/HrMcp.Agent/Components/Layout/SettingsModal.razor
git commit -m "feat(theme): add theme button group to Settings modal"
```
