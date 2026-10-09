using AquaTrack.Application.Ai;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Reporting;
using AquaTrack.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AquaTrack.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the use cases. Everything here depends only on Domain and on the abstractions in
    /// <c>Common.Interfaces</c>, so the composition root decides which infrastructure backs them.
    /// </summary>
    public static IServiceCollection AddAquaTrackApplication(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Farm and finance use cases
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

        // AI module
        services.AddScoped<IFeedAdvisor, FeedAdvisor>();
        services.AddScoped<IGrowthPredictor, GrowthPredictor>();
        services.AddScoped<IFinancePredictor, FinancePredictor>();
        services.AddScoped<IAnomalyDetector, AnomalyDetector>();
        services.AddScoped<ISmartAssistant, SmartAssistant>();

        // Report definitions; the exporter that renders them is infrastructure.
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
