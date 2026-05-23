// src/HrMcp.McpServer/Program.cs
using Azure.AI.OpenAI;
using Azure.Identity;
using HrMcp.Application.Services;
using HrMcp.Infrastructure.Persistence;
using HrMcp.McpServer.Tools;
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

var explicitStdio = args.Contains("--stdio");
var explicitStreamHttp = args.Contains("--stream-http");

var tempConfig = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile(
        $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
        optional: true)
    .AddEnvironmentVariables()
    .Build();

var configuredTransport = tempConfig["McpServer:Transport:Type"] ?? "stdio";
var useStdio = explicitStdio || (!explicitStreamHttp &&
    string.Equals(configuredTransport, "stdio", StringComparison.OrdinalIgnoreCase));

var enableDebug = args.Contains("--debug") ||
    tempConfig.GetValue<bool>("Features:EnableDebug");

var logConfig = new LoggerConfiguration().ReadFrom.Configuration(tempConfig);
if (enableDebug)
    logConfig = logConfig.MinimumLevel.Debug();

Log.Logger = logConfig
    .WriteTo.Conditional(_ => !useStdio, wt => wt.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    .WriteTo.Conditional(_ => useStdio, wt => wt.Console(
        standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose,
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    .CreateLogger();

try
{
    if (useStdio)
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

        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(() =>
        {
            var provider = hostBuilder.Configuration["AI:Provider"] ?? "Ollama";
            var model = string.Equals(provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase)
                ? hostBuilder.Configuration["AI:AzureOpenAI:Deployment"] ?? "unknown"
                : hostBuilder.Configuration["AI:Ollama:Model"] ?? "unknown";

            Console.Error.WriteLine("┌─────────────────────────────────────┐");
            Console.Error.WriteLine("│  HrMcp.McpServer                    │");
            Console.Error.WriteLine("│  Transport : stdio                  │");
            Console.Error.WriteLine($"│  Provider  : {provider,-23}│");
            Console.Error.WriteLine($"│  Model     : {model,-23}│");
            Console.Error.WriteLine("│  Status    : READY                  │");
            Console.Error.WriteLine("└─────────────────────────────────────┘");
        });

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

    var enableOidc = builder.Configuration.GetValue<bool>("Features:EnableOidc");

    if (enableOidc)
    {
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = builder.Configuration["Oidc:Authority"];
                options.Audience = builder.Configuration["Oidc:Audience"];
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

    var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    appLifetime.ApplicationStarted.Register(() =>
    {
        var provider = builder.Configuration["AI:Provider"] ?? "Ollama";
        var model = string.Equals(provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase)
            ? builder.Configuration["AI:AzureOpenAI:Deployment"] ?? "unknown"
            : builder.Configuration["AI:Ollama:Model"] ?? "unknown";
        var url = builder.Configuration["McpServer:Transport:StreamHttp:Url"] ?? "http://localhost:5100";

        Console.WriteLine("┌─────────────────────────────────────┐");
        Console.WriteLine("│  HrMcp.McpServer                    │");
        Console.WriteLine("│  Transport : stream-http             │");
        Console.WriteLine($"│  URL       : {url,-23}│");
        Console.WriteLine($"│  Provider  : {provider,-23}│");
        Console.WriteLine($"│  Model     : {model,-23}│");
        Console.WriteLine("│  Status    : READY                  │");
        Console.WriteLine("└─────────────────────────────────────┘");
    });

    if (enableOidc)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    var mcpPath = builder.Configuration["McpServer:Transport:StreamHttp:Path"] ?? "/mcp";
    var route = app.MapMcp(mcpPath);
    if (enableOidc)
        route.RequireAuthorization();

    await app.RunAsync();
}
catch (IOException ex) when (!useStdio && TryDescribePortConflict(ex, tempConfig, out var conflictMessage))
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
    services.AddSingleton<IChatClient>(_ => CreateChatClient(configuration));
}

static async Task InitializeDatabaseAsync(IServiceProvider services, bool forceReseed = false)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();

    await db.Database.MigrateAsync();

    var seedPath = FindSeedFile("data/usajobs-seed.json");
    DbSeeder.Seed(db, seedPath, force: forceReseed);
}

static string? FindSeedFile(string relativePath)
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, relativePath);
        if (File.Exists(candidate))
            return candidate;
    }

    return null;
}

static bool TryDescribePortConflict(IOException ex, IConfiguration configuration, out string message)
{
    var mcpUrl =
        configuration["McpServer:Transport:StreamHttp:Url"] ??
        configuration["Urls"] ??
        "http://localhost:5100";

    if (!Uri.TryCreate(mcpUrl.Split(';', StringSplitOptions.RemoveEmptyEntries)[0], UriKind.Absolute, out var uri))
    {
        message = "The MCP stream HTTP URL is invalid. Check McpServer:Transport:StreamHttp:Url or Urls.";
        return true;
    }

    if (!ex.Message.Contains(uri.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
    {
        message = string.Empty;
        return false;
    }

    var conflict = IPGlobalProperties.GetIPGlobalProperties()
        .GetActiveTcpListeners()
        .FirstOrDefault(endpoint =>
            endpoint.Port == uri.Port &&
            (IPAddress.IsLoopback(endpoint.Address) || endpoint.Address.Equals(IPAddress.Any) || endpoint.Address.Equals(IPAddress.IPv6Any)));

    if (conflict is null)
    {
        message =
            $"Port {uri.Port} is already in use. Stop the existing listener or change the configured MCP server URL.";
        return true;
    }

    message =
        $"Port {uri.Port} is already in use. Stop the running server or change the configured URL before starting another instance.";
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
