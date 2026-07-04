// src/HrMcp.Infrastructure.Persistence/DependencyInjection.cs
<<<<<<< HEAD
using HrMcp.Core.Interfaces;
using HrMcp.Infrastructure.Persistence.Repositories;
=======
using HrMcp.Core.Entities;
using HrMcp.Core.Interfaces;
using HrMcp.Infrastructure.Persistence.Repositories;
using HrMcp.Infrastructure.Persistence.Services;
using Microsoft.AspNetCore.Identity;
>>>>>>> release/v1
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrMcp.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        string connectionString)
    {
<<<<<<< HEAD
        services.AddDbContext<HrDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IHiringOrganizationRepository, HiringOrganizationRepository>();
=======
        services.AddDbContextFactory<HrDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<HrDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IHiringOrganizationRepository, HiringOrganizationRepository>();
        services.AddScoped<IConversationService, ConversationService>();
>>>>>>> release/v1

        return services;
    }
}
