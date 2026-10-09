using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Reporting;
using AquaTrack.Infrastructure.Identity;
using AquaTrack.Infrastructure.Persistence;
using AquaTrack.Infrastructure.Reporting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AquaTrack.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the outward-facing concerns: the database, Identity, and document rendering.
    /// Each is bound to the abstraction the application layer declared, so swapping any of them
    /// does not touch a use case.
    /// </summary>
    public static IServiceCollection AddAquaTrackInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AquaTrackDbContext>(options => ConfigureProvider(options, configuration));

        // Application code resolves the interface; only migrations and seeding use the concrete type.
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AquaTrackDbContext>());

        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<AquaTrackDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IReportExporter, ReportExporter>();

        return services;
    }

    /// <summary>
    /// Picks the database provider from configuration, so the same build runs on SQLite for a
    /// quick local start or SQL Server for a server deployment.
    /// </summary>
    private static void ConfigureProvider(DbContextOptionsBuilder options, IConfiguration configuration)
    {
        var provider = configuration.GetValue("Database:Provider", "Sqlite")!;
        var connectionString = configuration.GetConnectionString(provider)
                               ?? configuration.GetConnectionString("Default")
                               ?? "Data Source=aquatrack.db";

        switch (provider.ToLowerInvariant())
        {
            case "sqlserver":
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
                break;

            case "sqlite":
            default:
                options.UseSqlite(connectionString);
                break;
        }
    }
}
