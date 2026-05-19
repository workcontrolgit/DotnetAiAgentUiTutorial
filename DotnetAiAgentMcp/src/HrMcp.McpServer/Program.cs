// src/HrMcp.McpServer/Program.cs
using HrMcp.Application.Services;
using HrMcp.Infrastructure.Persistence;
using HrMcp.McpServer.Tools;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OllamaSharp;
using Serilog;
using System.Net;
using System.Net.NetworkInformation;

var isStdio = args.Contains("--stdio");

// Bootstrap Serilog from appsettings (file sinks).
// Console sink is added conditionally: suppressed in stdio mode so stdout
// carries only JSON-RPC messages.
var tempConfig = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile(
        $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
        optional: true)
    .AddEnvironmentVariables()
    .Build();

// Resolve log path relative to the app binary so it is consistent
// regardless of which directory dotnet run is invoked from.
// Anchor log paths to the app binary directory so location is consistent
// regardless of which directory dotnet run is invoked from.
//   logs/info/info-YYYYMMDD.log  — Information and above
//   logs/error/error-YYYYMMDD.log — Error and Fatal only
var logBase = Path.Combine(AppContext.BaseDirectory, "logs");

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(tempConfig)
    .WriteTo.Conditional(_ => !isStdio, wt => wt.Console())
    .WriteTo.File(
        Path.Combine(logBase, "info", "info-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(logBase, "error", "error-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    if (isStdio)
    {
        var hostBuilder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
        hostBuilder.Services.AddSerilog();

        ConfigureCommonServices(hostBuilder.Services, hostBuilder.Configuration);

        hostBuilder.Services
            .AddMcpServer()
            .WithTools<PositionTools>()
            .WithTools<HiringOrganizationTools>()
            .WithTools<JobDescriptionTools>()
            .WithStdioServerTransport();

        using var host = hostBuilder.Build();
        await InitializeDatabaseAsync(host.Services, args.Contains("--reseed"));
        await host.RunAsync();
        return;
    }

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });
    builder.Host.UseSerilog();

    ConfigureCommonServices(builder.Services, builder.Configuration);

    // ── OIDC feature flag ────────────────────────────────────────────────────
    // Set Features:EnableOidc = false in appsettings.Development.json to run
    // without an identity provider (e.g. when testing with MCP Inspector).
    // Set to true in production to enforce JWT Bearer authentication.
    var enableOidc = builder.Configuration.GetValue<bool>("Features:EnableOidc");

    if (enableOidc)
    {
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = builder.Configuration["Oidc:Authority"];
                options.Audience  = builder.Configuration["Oidc:Audience"];
                // Trust self-signed certs when running against a local dev IdentityServer container
                if (builder.Environment.IsDevelopment())
                    options.BackchannelHttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    };
            });
        builder.Services.AddAuthorization();
    }

    builder.Services
        .AddMcpServer()
        .WithTools<PositionTools>()
        .WithTools<HiringOrganizationTools>()
        .WithTools<JobDescriptionTools>()
        .WithHttpTransport();

    var app = builder.Build();
    await InitializeDatabaseAsync(app.Services, args.Contains("--reseed"));

    if (enableOidc)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    if (!isStdio)
    {
        var route = app.MapMcp("/mcp");
        if (enableOidc)
            route.RequireAuthorization();
    }

    await app.RunAsync();
}
catch (IOException ex) when (!isStdio && TryDescribePortConflict(ex, out var conflictMessage))
{
    Log.Fatal(ex, "{ConflictMessage}", conflictMessage);
    Console.Error.WriteLine(conflictMessage);
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static void ConfigureCommonServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddPersistence(
        configuration.GetConnectionString("DefaultConnection")!);
    services.AddScoped<PositionService>();
    services.AddScoped<HiringOrganizationService>();

    // IChatClient used by WriteJobDescription tool to generate LLM narratives.
    services.AddSingleton<IChatClient>(_ => CreateChatClient(configuration));
}

static async Task InitializeDatabaseAsync(IServiceProvider services, bool forceReseed = false)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();

    await db.Database.MigrateAsync();

    // Walk up from the binary directory to find data/usajobs-seed.json
    // (works whether invoked via dotnet run, published exe, or Claude Desktop stdio)
    var seedPath = FindSeedFile("data/usajobs-seed.json");
    DbSeeder.Seed(db, seedPath, force: forceReseed);
}

// Searches from AppContext.BaseDirectory upward for a relative path (e.g. "data/usajobs-seed.json").
// Returns the full path when found, or null if not found within 8 levels.
static string? FindSeedFile(string relativePath)
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, relativePath);
        if (File.Exists(candidate)) return candidate;
    }
    return null;
}

static bool TryDescribePortConflict(IOException ex, out string message)
{
    const int defaultPort = 5100;
    const string defaultUrl = "http://127.0.0.1:5100";

    if (!ex.Message.Contains(defaultUrl, StringComparison.OrdinalIgnoreCase) &&
        !ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
    {
        message = string.Empty;
        return false;
    }

    var conflict = IPGlobalProperties.GetIPGlobalProperties()
        .GetActiveTcpListeners()
        .FirstOrDefault(endpoint =>
            endpoint.Port == defaultPort &&
            IPAddress.IsLoopback(endpoint.Address));

    if (conflict is null)
    {
        message =
            $"Port {defaultPort} is already in use. Stop the existing listener or change the configured MCP server URL.";
        return true;
    }

    message =
        $"Port {defaultPort} is already in use on a loopback interface. Stop the running server or change the configured URL before starting another instance.";
    return true;
}

static IChatClient CreateChatClient(IConfiguration configuration)
{
    var provider = configuration["AI:Provider"] ?? "Ollama";

    if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
    {
        var endpoint = configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434";
        var model = configuration["AI:Ollama:Model"] ?? "llama3.2";

        return (IChatClient)new OllamaApiClient(new Uri(endpoint), model);
    }

    var azureEndpoint = configuration["AI:AzureOpenAI:Endpoint"]
        ?? throw new InvalidOperationException("Missing configuration: AI:AzureOpenAI:Endpoint");
    var azureDeployment = configuration["AI:AzureOpenAI:Deployment"]
        ?? throw new InvalidOperationException("Missing configuration: AI:AzureOpenAI:Deployment");
    var apiKey = configuration["AI:AzureOpenAI:ApiKey"];

    var client = string.IsNullOrWhiteSpace(apiKey)
        ? new AzureOpenAIClient(new Uri(azureEndpoint), new DefaultAzureCredential())
        : new AzureOpenAIClient(new Uri(azureEndpoint), new System.ClientModel.ApiKeyCredential(apiKey));

    return client.GetChatClient(azureDeployment).AsIChatClient();
}
