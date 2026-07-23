// src/HrMcp.McpServer/Program.cs
using HrMcp.Application.Services;
using HrMcp.Infrastructure.Persistence;
using HrMcp.McpServer.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            .WithTools<ExportTools>()
            .WithStdioServerTransport();

        using var host = hostBuilder.Build();
        await InitializeDatabaseAsync(host.Services, args.Contains("--reseed"));

        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(() =>
        {
            Console.Error.WriteLine("┌─────────────────────────────────────┐");
            Console.Error.WriteLine("│  HrMcp.McpServer                    │");
            Console.Error.WriteLine("│  Transport : stdio                  │");
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
        .WithTools<ExportTools>()
        .WithHttpTransport();

    var app = builder.Build();
    await InitializeDatabaseAsync(app.Services, args.Contains("--reseed"));

    var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    appLifetime.ApplicationStarted.Register(() =>
    {
        var url = builder.Configuration["McpServer:Transport:StreamHttp:Url"] ?? "http://localhost:5100";
        Console.WriteLine("┌─────────────────────────────────────┐");
        Console.WriteLine("│  HrMcp.McpServer                    │");
        Console.WriteLine("│  Transport : stream-http             │");
        Console.WriteLine($"│  URL       : {url,-23}│");
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
}

static async Task InitializeDatabaseAsync(IServiceProvider services, bool forceReseed = false)
{
    var seedPath = FindSeedFile("data/usajobs-seed.json");
    await PersistenceInitialization.InitializeAsync(services, seedPath, forceReseed);
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
