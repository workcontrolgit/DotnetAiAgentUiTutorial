using HrMcp.Agent.Web.Services;
using Microsoft.JSInterop;
using Xunit;

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

        private ValueTask<TValue> Handle<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "theme.get")
                return ValueTask.FromResult((TValue)(object)storedTheme);

            if (identifier == "theme.set" && args?.Length > 0)
                LastSetName = args[0]?.ToString();

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => Handle<TValue>(identifier, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => Handle<TValue>(identifier, args);
    }
}
