// src/HrMcp.Agent/Program.cs
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using HrMcp.Agent;

// Connect to the MCP server (must be running on http://localhost:5100)
await using var mcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri("http://localhost:5100/mcp")
    }));

var mcpTools = await mcpClient.ListToolsAsync();
Console.WriteLine($"Connected. Tools: {string.Join(", ", mcpTools.Select(t => t.Name))}\n");

// Ollama chat client with automatic function-call middleware
IChatClient chatClient = new OllamaChatClient(
        new Uri("http://localhost:11434"), "llama3.2")
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList());
await agent.RunAsync();
