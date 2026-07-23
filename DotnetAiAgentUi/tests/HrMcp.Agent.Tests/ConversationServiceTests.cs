// DotnetAiAgentUi/tests/HrMcp.Agent.Tests/ConversationServiceTests.cs
using HrMcp.Core.Interfaces;
using HrMcp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HrMcp.Agent.Tests;

public sealed class ConversationServiceTests : IDisposable
{
    private readonly HrDbContext _db;
    private readonly IConversationService _sut;
    private readonly ServiceProvider _serviceProvider;

    public ConversationServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContextFactory<HrDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName));
        _serviceProvider = services.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<HrDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        _db = new HrDbContext(options);

        var factory = _serviceProvider.GetRequiredService<IDbContextFactory<HrDbContext>>();
        _sut = new HrMcp.Infrastructure.Persistence.Services.ConversationService(factory);
    }

    [Fact]
    public async Task CreateSessionAsync_StoresSessionWithTruncatedName()
    {
        var session = await _sut.CreateSessionAsync("user1", "Draft a job description for a software engineer role", default);

        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal("user1", session.UserId);
        Assert.True(session.Name.Length <= 50);
        Assert.StartsWith("Draft a job description", session.Name);
    }

    [Fact]
    public async Task GetSessionsAsync_ReturnsOnlyUserSessions()
    {
        await _sut.CreateSessionAsync("user1", "First session", default);
        await _sut.CreateSessionAsync("user2", "Other user session", default);

        var sessions = await _sut.GetSessionsAsync("user1", default);

        Assert.Single(sessions);
        Assert.Equal("user1", sessions[0].UserId);
    }

    [Fact]
    public async Task AddTurnAsync_AppendsTurnToSession()
    {
        var session = await _sut.CreateSessionAsync("user1", "Hello", default);

        await _sut.AddTurnAsync(session.Id, "user1", "user", "Hello", default);
        await _sut.AddTurnAsync(session.Id, "user1", "assistant", "Hi there!", default);

        var loaded = await _sut.GetSessionAsync(session.Id, "user1", default);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Turns.Count);
    }

    [Fact]
    public async Task AddTurnAsync_IgnoresTurnForWrongUser()
    {
        var session = await _sut.CreateSessionAsync("user1", "Hello", default);

        await _sut.AddTurnAsync(session.Id, "user2", "user", "Should be ignored", default);

        var loaded = await _sut.GetSessionAsync(session.Id, "user1", default);
        Assert.NotNull(loaded);
        Assert.Empty(loaded!.Turns);
    }

    [Fact]
    public async Task GetSessionAsync_ReturnsNullForWrongUser()
    {
        var session = await _sut.CreateSessionAsync("user1", "My session", default);

        var result = await _sut.GetSessionAsync(session.Id, "user2", default);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteSessionAsync_RemovesSession()
    {
        var session = await _sut.CreateSessionAsync("user1", "To delete", default);

        await _sut.DeleteSessionAsync(session.Id, "user1", default);

        var result = await _sut.GetSessionAsync(session.Id, "user1", default);
        Assert.Null(result);
    }

    public void Dispose()
    {
        _db.Dispose();
        _serviceProvider.Dispose();
    }
}

public sealed class PersistenceInitializationTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;

    public PersistenceInitializationTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContextFactory<HrDbContext>(options => options.UseSqlite(_connection));
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task InitializeAsync_AppliesMigrationsThatCreateConversationTables()
    {
        await PersistenceInitialization.InitializeAsync(_serviceProvider);

        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'ConversationSessions'";

        var result = await command.ExecuteScalarAsync();

        Assert.Equal("ConversationSessions", result);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
    }
}
