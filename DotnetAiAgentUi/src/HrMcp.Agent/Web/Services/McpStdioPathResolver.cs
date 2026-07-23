namespace HrMcp.Agent.Web.Services;

internal static class McpStdioPathResolver
{
    public static (string WorkingDirectory, string ProjectPath) Resolve(
        string? configuredWorkingDirectory,
        string? configuredProjectPath,
        string workspaceRoot)
    {
        var workingDirectory = configuredWorkingDirectory;
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
            workingDirectory = workspaceRoot;

        if (!string.IsNullOrWhiteSpace(configuredProjectPath) && File.Exists(configuredProjectPath))
            return (workingDirectory, configuredProjectPath);

        var projectCandidates = new[]
        {
            Path.Combine(workingDirectory, "DotnetAiAgentUi", "src", "HrMcp.McpServer", "HrMcp.McpServer.csproj"),
            Path.Combine(workingDirectory, "src", "HrMcp.McpServer", "HrMcp.McpServer.csproj")
        };

        var resolvedProjectPath = projectCandidates.FirstOrDefault(File.Exists) ?? projectCandidates[0];
        return (workingDirectory, resolvedProjectPath);
    }
}