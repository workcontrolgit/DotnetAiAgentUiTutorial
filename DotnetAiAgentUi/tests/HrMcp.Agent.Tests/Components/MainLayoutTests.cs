using Bunit;
using HrMcp.Agent.Components.Layout;
using HrMcp.Agent.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HrMcp.Agent.Tests.Components;

public sealed class MainLayoutTests : TestContext
{
    public MainLayoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped<ThemeService>();
        ComponentFactories.AddStub<SessionsSidebar>();
    }

    private IRenderedComponent<MainLayout> Render()
    {
        return RenderComponent<MainLayout>(p => p
            .Add(c => c.Body, builder => builder.AddMarkupContent(0, "<div class='body-content'>body</div>")));
    }

    [Fact]
    public void Renders_MainLayout_Container()
    {
        var cut = Render();
        cut.Find(".main-layout");
    }

    [Fact]
    public void Renders_MainContent_Wrapper()
    {
        var cut = Render();
        cut.Find(".main-content");
    }

    [Fact]
    public void Body_RendersInside_MainContent()
    {
        var cut = Render();
        var content = cut.Find(".main-content");
        Assert.Contains("body-content", content.InnerHtml);
    }
}
