using Bunit;
using HrMcp.Agent.Components.Layout;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HrMcp.Agent.Tests.Components;

public sealed class MainLayoutTests : TestContext
{
    private static IConfiguration AzureConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AI:Provider", "AzureOpenAI" },
                { "AI:AzureOpenAI:Deployment", "gpt-4.1-mini" },
                { "AI:Ollama:Model", "gemma4:latest" },
                { "McpServer:Transport:Type", "stdio" }
            })
            .Build();

    private static IConfiguration OllamaConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AI:Provider", "Ollama" },
                { "AI:AzureOpenAI:Deployment", "gpt-4.1-mini" },
                { "AI:Ollama:Model", "gemma4:latest" },
                { "McpServer:Transport:Type", "stdio" }
            })
            .Build();

    private IRenderedComponent<MainLayout> Render(IConfiguration? config = null)
    {
        Services.AddSingleton<IConfiguration>(config ?? AzureConfig());
        return RenderComponent<MainLayout>(p => p
            .Add(c => c.Body, builder => builder.AddMarkupContent(0, "<div>body</div>")));
    }

    [Fact]
    public void Renders_AppShell_WithSidebar()
    {
        var cut = Render();
        cut.Find(".app-shell");
        cut.Find(".history-sidebar");
    }

    [Fact]
    public void Sidebar_CollapsesOnHamburgerClick()
    {
        var cut = Render();
        cut.Find(".icon-btn").Click();
        Assert.Contains("history-sidebar--collapsed", cut.Find(".history-sidebar").ClassList);
    }

    [Fact]
    public void Sidebar_ExpandsOnSecondHamburgerClick()
    {
        var cut = Render();
        cut.Find(".icon-btn").Click();
        cut.Find(".icon-btn").Click();
        Assert.DoesNotContain("history-sidebar--collapsed", cut.Find(".history-sidebar").ClassList);
    }

    [Fact]
    public void SettingsModal_OpenOnGearClick()
    {
        var cut = Render();
        cut.Find(".ghost-btn").Click();
        cut.Find(".modal-card");
    }

    [Fact]
    public void SettingsModal_ClosesOnXClick()
    {
        var cut = Render();
        cut.Find(".ghost-btn").Click();
        cut.Find(".modal-close-btn").Click();
        Assert.Empty(cut.FindAll(".modal-card"));
    }

    [Fact]
    public void SettingsModal_ClosesOnBackdropClick()
    {
        var cut = Render();
        cut.Find(".ghost-btn").Click();
        cut.Find(".modal-overlay").Click();
        Assert.Empty(cut.FindAll(".modal-card"));
    }

    [Fact]
    public void SettingsModal_ShowsAzureModel()
    {
        var cut = Render(AzureConfig());
        cut.Find(".ghost-btn").Click();
        Assert.Contains("gpt-4.1-mini", cut.Find(".modal-card").TextContent);
    }

    [Fact]
    public void SettingsModal_ShowsOllamaModel()
    {
        var cut = Render(OllamaConfig());
        cut.Find(".ghost-btn").Click();
        Assert.Contains("gemma4:latest", cut.Find(".modal-card").TextContent);
    }

    [Fact]
    public void Sidebar_ShowsSignInPlaceholder()
    {
        var cut = Render();
        var btn = cut.Find(".sidebar-user-btn");
        Assert.Contains("Sign in", btn.TextContent);
        Assert.True(btn.HasAttribute("disabled"));
    }

    [Fact]
    public void Sidebar_ShowsNoHistoryPlaceholder()
    {
        var cut = Render();
        Assert.Contains("No history yet", cut.Find(".sidebar-history").TextContent);
    }
}
