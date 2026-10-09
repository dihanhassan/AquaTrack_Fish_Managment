using AquaTrack.Infrastructure.Services;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.Controllers;

public class DashboardController(
    IDashboardService dashboard,
    INotificationService notifications,
    ISettingsService settings) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Re-run the detectors on each dashboard load so alerts stay current without a scheduler.
        await notifications.RefreshAsync(ct);

        var model = await BuildAsync(today, ct);

        ViewData["Title"] = "Dashboard";
        ViewData["Subtitle"] = today.ToString("dddd, dd MMMM yyyy");
        return View(model);
    }

    /// <summary>Returns just the KPI row, so the dashboard can refresh figures without a full reload.</summary>
    [HttpGet]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var model = await BuildAsync(today, ct);

        return PartialView("_SummaryCards", model);
    }

    private async Task<DashboardViewModel> BuildAsync(DateOnly today, CancellationToken ct)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        return new DashboardViewModel
        {
            Today = today,
            CurrencySymbol = await settings.GetAsync(SettingKeys.CurrencySymbol, "Tk", ct),
            Summary = await dashboard.GetSummaryAsync(today, ct),
            DailySales = await dashboard.GetDailySalesAsync(14, ct),
            IncomeVsExpense = await dashboard.GetIncomeVsExpenseAsync(6, ct),
            ExpenseByCategory = await dashboard.GetExpenseByCategoryAsync(monthStart, today, ct),
            FeedUsageByPond = await dashboard.GetFeedUsageByPondAsync(monthStart, today, ct),
            ProductionByPond = await dashboard.GetProductionByPondAsync(today.AddMonths(-6), today, ct),
            RecentAlerts = await notifications.GetRecentAsync(6, ct),
        };
    }
}
