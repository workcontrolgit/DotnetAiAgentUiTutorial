// src/HrMcp.Infrastructure.Persistence/HrDbContextFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HrMcp.Infrastructure.Persistence;

/// <summary>
/// Design-time DbContext factory for Entity Framework Core migrations.
/// Used by EF Core tools (dotnet ef) to create DbContext instances during migrations.
/// </summary>
public class HrDbContextFactory : IDesignTimeDbContextFactory<HrDbContext>
{
    public HrDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HrDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=HrMcpDb;Integrated Security=true;");

        return new HrDbContext(optionsBuilder.Options);
    }
}
