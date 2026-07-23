using HrMcp.Agent.Web.Services;
using Xunit;

namespace HrMcp.Agent.Tests.Logic;

public sealed class McpStdioPathResolverTests
{
    [Fact]
    public void Resolve_InvalidWorkingDirectory_FallsBackToWorkspaceRoot()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "mcp-resolver-workspace-" + Guid.NewGuid());
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var result = McpStdioPathResolver.Resolve(
                configuredWorkingDirectory: Path.Combine(workspaceRoot, "does-not-exist"),
                configuredProjectPath: null,
                workspaceRoot: workspaceRoot);

            Assert.Equal(workspaceRoot, result.WorkingDirectory);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ValidConfiguredProjectPath_IsPreserved()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "mcp-resolver-workspace-" + Guid.NewGuid());
        var projectDir = Path.Combine(workspaceRoot, "src", "HrMcp.McpServer");
        var projectPath = Path.Combine(projectDir, "HrMcp.McpServer.csproj");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        try
        {
            var result = McpStdioPathResolver.Resolve(
                configuredWorkingDirectory: workspaceRoot,
                configuredProjectPath: projectPath,
                workspaceRoot: workspaceRoot);

            Assert.Equal(projectPath, result.ProjectPath);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public void Resolve_MissingProjectPath_UsesSrcFallbackUnderWorkspaceRoot()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "mcp-resolver-workspace-" + Guid.NewGuid());
        var projectDir = Path.Combine(workspaceRoot, "src", "HrMcp.McpServer");
        var expectedProjectPath = Path.Combine(projectDir, "HrMcp.McpServer.csproj");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(expectedProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        try
        {
            var result = McpStdioPathResolver.Resolve(
                configuredWorkingDirectory: workspaceRoot,
                configuredProjectPath: null,
                workspaceRoot: workspaceRoot);

            Assert.Equal(expectedProjectPath, result.ProjectPath);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }
}
