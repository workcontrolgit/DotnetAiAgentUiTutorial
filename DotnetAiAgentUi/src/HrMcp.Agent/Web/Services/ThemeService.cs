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
