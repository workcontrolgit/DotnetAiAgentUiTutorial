// src/HrMcp.Infrastructure.Persistence/HrDbContext.cs
using HrMcp.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HrMcp.Infrastructure.Persistence;

public class HrDbContext(DbContextOptions<HrDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<HiringOrganization>   HiringOrganizations   => Set<HiringOrganization>();
    public DbSet<Position>             Positions             => Set<Position>();
    public DbSet<PositionRemuneration> PositionRemunerations => Set<PositionRemuneration>();
    public DbSet<ConversationSession>  ConversationSessions  => Set<ConversationSession>();
    public DbSet<ConversationTurn>     ConversationTurns     => Set<ConversationTurn>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PositionRemuneration>()
            .Property(r => r.MinimumRange).HasPrecision(18, 2);
        modelBuilder.Entity<PositionRemuneration>()
            .Property(r => r.MaximumRange).HasPrecision(18, 2);

        modelBuilder.Entity<PositionRemuneration>()
            .HasOne(r => r.Position)
            .WithOne(p => p.PositionRemuneration)
            .HasForeignKey<PositionRemuneration>(r => r.PositionId);

        modelBuilder.Entity<ConversationSession>()
            .HasIndex(s => s.UserId);

        modelBuilder.Entity<ConversationTurn>()
            .HasOne(t => t.Session)
            .WithMany(s => s.Turns)
            .HasForeignKey(t => t.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
