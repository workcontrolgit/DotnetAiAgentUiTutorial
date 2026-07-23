using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrMcp.Infrastructure.Persistence;

public static class PersistenceInitialization
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        string? seedPath = null,
        bool forceReseed = false,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<HrDbContext>>();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        if (db.Database.IsSqlServer())
            await db.Database.MigrateAsync(cancellationToken);
        else
            await db.Database.EnsureCreatedAsync(cancellationToken);

        DbSeeder.Seed(db, seedPath, force: forceReseed);
    }
}