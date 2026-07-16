# Theme Switcher Design

**Date:** 2026-07-16
**Feature:** Configurable UI theme (Light / Dark / Sepia) with localStorage persistence

---

## Summary

Replace the hardcoded Catppuccin Mocha dark theme with a runtime-switchable theme system. Three themes ship initially: **Light** (Catppuccin Latte — new default), **Dark** (Catppuccin Mocha — current), and **Sepia**. The user selects their theme in the Settings modal; the choice persists via `localStorage`.

---

## Approach

**CSS data-attribute theming.** A `data-theme` attribute on `<html>` controls which CSS token block is active. Each theme is a `:root[data-theme="..."]` block in `app.css`. Switching themes sets the attribute and writes to `localStorage` — no page reload, no server roundtrip.

---

## CSS Tokens

All three themes define the same set of CSS custom properties. Existing component styles reference only the token variables, so they adapt automatically.

### Token Values

| Token        | Light (Latte) | Dark (Mocha) | Sepia      |
|--------------|---------------|--------------|------------|
| `--base`     | `#eff1f5`     | `#1e1e2e`   | `#f4f0e8`  |
| `--mantle`   | `#e6e9ef`     | `#181825`   | `#ede8dc`  |
| `--crust`    | `#dce0e8`     | `#11111b`   | `#e3ddd0`  |
| `--surface0` | `#ccd0da`     | `#313244`   | `#d5cfc2`  |
| `--surface1` | `#bcc0cc`     | `#45475a`   | `#c8c2b4`  |
| `--surface2` | `#acb0be`     | `#585b70`   | `#b8b2a4`  |
| `--overlay1` | `#8c8fa1`     | `#7f849c`   | `#8a8070`  |
| `--subtext0` | `#6c6f85`     | `#a6adc8`   | `#6a6055`  |
| `--text`     | `#4c4f69`     | `#cdd6f4`   | `#3c3228`  |
| `--blue`     | `#1e66f5`     | `#89b4fa`   | `#7c5c3a`  |
| `--blue-dark`| `#1a5ce0`     | `#6495f4`   | `#6b4e30`  |
| `--border`   | `#ccd0da`     | `#313244`   | `#d5cfc2`  |
| `--red`      | `#d20f39`     | `#f38ba8`   | `#a0402a`  |

### CSS Structure in `app.css`

```css
/* existing :root block becomes the dark fallback — renamed: */
:root[data-theme="dark"] { ... }

/* new default (applied if no localStorage value): */
:root, :root[data-theme="light"] { ... }

:root[data-theme="sepia"] { ... }
```

The bare `:root` selector ensures light tokens are active before JS runs (no flash of dark theme on first load).

---

## ThemeService

**File:** `HrMcp.Agent/Web/Services/ThemeService.cs`

```csharp
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

Registered as **scoped** in `Program.cs`.

---

## JS Interop

**File:** `HrMcp.Agent/wwwroot/js/theme.js`

```js
window.theme = {
  get: () => localStorage.getItem('hr-theme') ?? 'light',
  set: (name) => {
    localStorage.setItem('hr-theme', name);
    document.documentElement.setAttribute('data-theme', name);
  }
};
```

Referenced via a `<script>` tag in `Components/App.razor` (or `wwwroot/index.html` if present).

---

## MainLayout Integration

`MainLayout.razor` injects `ThemeService` and calls `InitAsync` in `OnAfterRenderAsync(firstRender: true)`. This applies the saved theme as early as Blazor's interactive lifecycle allows, avoiding a flash.

---

## Settings Modal UI

A **Theme** row is added to the settings table in `SettingsModal.razor`, above existing rows:

```
Theme    [ Light ]  [ Dark ]  [ Sepia ]
```

- Rendered as a `.theme-btn-group` containing three `.theme-btn` buttons
- Active theme button: border colored with `--blue`, slightly elevated background
- Inactive buttons: styled like `.ghost-btn`
- Clicking calls `ThemeService.SetThemeAsync(js, name)`; `StateHasChanged` is not required — the CSS variable swap repaints instantly

### New CSS classes (added to `app.css`)

```css
.theme-btn-group {
    display: flex;
    gap: 6px;
}

.theme-btn {
    /* inherits ghost-btn base styles */
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

---

## Quill Editor

The Quill dark-theme overrides in `app.css` already use token variables (`var(--subtext0)`, `var(--text)`, etc.). No changes needed — they adapt automatically across all themes.

---

## Files Changed

| File | Change |
|------|--------|
| `wwwroot/css/app.css` | Rename `:root` to `dark` block; add `light` (default) and `sepia` blocks; add `.theme-btn*` classes |
| `wwwroot/js/theme.js` | New file — localStorage get/set + data-theme attribute |
| `Components/App.razor` | Add `<script src="js/theme.js">` |
| `Web/Services/ThemeService.cs` | New scoped service |
| `Program.cs` | Register `ThemeService` as scoped |
| `Components/Layout/MainLayout.razor` | Inject service, call `InitAsync` on first render |
| `Components/Layout/SettingsModal.razor` | Add Theme row with button group |

---

## Out of Scope

- Per-user theme stored in the database (localStorage is sufficient)
- System `prefers-color-scheme` auto-detection (can be added later as a fourth "Auto" option)
- Theme preview thumbnails in the Settings modal
