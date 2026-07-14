# start-stream-http.ps1
# Starts HrMcp.McpServer (stream-http) in a new window, then runs HrMcp.Agent
# in the current window. The agent polls for the server automatically - no manual
# delay needed.
#
# Usage (from repo root or scripts/ folder):
#   .\scripts\start-stream-http.ps1
#   .\scripts\start-stream-http.ps1 -NumCtx 65536

param(
    [int]$NumCtx = 0
)

$RepoRoot     = Split-Path -Parent $PSScriptRoot
$McpProject   = Join-Path $RepoRoot "DotnetAiAgentUi\src\HrMcp.McpServer\HrMcp.McpServer.csproj"
$AgentProject = Join-Path $RepoRoot "DotnetAiAgentUi\src\HrMcp.Agent\HrMcp.Agent.csproj"

if (-not (Test-Path $McpProject)) {
    Write-Error "McpServer project not found: $McpProject"
    exit 1
}
if (-not (Test-Path $AgentProject)) {
    Write-Error "Agent project not found: $AgentProject"
    exit 1
}

# Build the dotnet command as a plain string — no backtick escaping inside strings
$McpCmd = 'dotnet run --project "' + $McpProject + '" -- --stream-http'
$TitleAndRun = '$Host.UI.RawUI.WindowTitle = "HrMcp.McpServer (stream-http)"; ' + $McpCmd

# Launch MCP server in a new window
Start-Process powershell -ArgumentList "-NoExit", "-Command", $TitleAndRun

Write-Host "McpServer starting in new window - agent will wait for it to be ready." -ForegroundColor Cyan
Write-Host ""

# Build agent args
$AgentArgs = @("--stream-http")
if ($NumCtx -gt 0) {
    $AgentArgs += "--num-ctx"
    $AgentArgs += $NumCtx
}

# Run agent interactively in this window
& dotnet run --project $AgentProject -- @AgentArgs
