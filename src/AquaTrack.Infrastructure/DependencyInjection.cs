using AquaTrack.Infrastructure.Ai;
using AquaTrack.Infrastructure.Identity;
using AquaTrack.Infrastructure.Persistence;
using AquaTrack.Infrastructure.Reporting;
using AquaTrack.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AquaTrack.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence, identity, business services and the AI module.
    /// The database provider is chosen by configuration so the same build runs on SQLite or SQL Server.
    /// </summary>
    public static IServiceCollection AddAquaTrackInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddDbContext<AquaTrackDbContext>(options => ConfigureProvider(options, configuration));

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

        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IDocumentNumberService, DocumentNumberService>();
        services.AddScoped<ICashBookService, CashBookService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<INotificationService, NotificationService>();

        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IDailyLogService, DailyLogService>();

        services.AddScoped<IFeedAdvisor, FeedAdvisor>();
        services.AddScoped<IGrowthPredictor, GrowthPredictor>();
        services.AddScoped<IFinancePredictor, FinancePredictor>();
        services.AddScoped<IAnomalyDetector, AnomalyDetector>();
        services.AddScoped<ISmartAssistant, SmartAssistant>();

        services.AddScoped<IReportExporter, ReportExporter>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }

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
