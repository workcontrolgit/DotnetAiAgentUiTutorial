// src/HrMcp.Infrastructure.Persistence/DependencyInjection.cs
using HrMcp.Core.Interfaces;
using HrMcp.Infrastructure.Persistence.Repositories;
using HrMcp.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrMcp.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<HrDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IHiringOrganizationRepository, HiringOrganizationRepository>();
        services.AddScoped<IConversationService, ConversationService>();

        return services;
    }
}
