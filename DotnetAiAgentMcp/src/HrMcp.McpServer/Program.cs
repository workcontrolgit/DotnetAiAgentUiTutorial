// src/HrMcp.McpServer/Program.cs
using HrMcp.Application.Services;
using HrMcp.Infrastructure.Persistence;
using HrMcp.McpServer.Tools;
using Microsoft.EntityFrameworkCore;

var isStdio = args.Contains("--stdio");

var builder = WebApplication.CreateBuilder(args);

// Stdout must contain only JSON-RPC when running as a stdio server.
// Clear all log providers so nothing leaks into stdout.
if (isStdio)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = Microsoft.Extensions.Logging.LogLevel.Trace);
    builder.WebHost.UseUrls(); // no HTTP listener in stdio mode
}

builder.Services.AddPersistence(
    builder.Configuration.GetConnectionString("DefaultConnection")!);
builder.Services.AddScoped<PositionService>();
builder.Services.AddScoped<HiringOrganizationService>();

var mcp = builder.Services
    .AddMcpServer()
    .WithTools<PositionTools>()
    .WithTools<HiringOrganizationTools>()
    .WithTools<JobDescriptionTools>();

if (isStdio)
    mcp.WithStdioServerTransport();
else
    mcp.WithHttpTransport();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
    db.Database.Migrate();

    // Looks for data/usajobs-seed.json in the working directory (solution root when using dotnet run)
    var seedPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "usajobs-seed.json");
    DbSeeder.Seed(db, seedPath);
}

if (!isStdio)
    app.MapMcp("/mcp");

await app.RunAsync();
