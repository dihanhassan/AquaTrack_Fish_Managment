using AquaTrack.Application.Ai;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// The AI module's read-only surface: feed advice, growth forecasts, financial forecasts and the
/// anomalies behind the alerts. Every figure arrives with the reasoning that produced it.
/// </summary>
public class AiController(
    IApplicationDbContext db,
    IFeedAdvisor feedAdvisor,
    IGrowthPredictor growthPredictor,
    IFinancePredictor financePredictor,
    IAnomalyDetector anomalyDetector) : AppController
{
    public async Task<IActionResult> Index(int? pondId, CancellationToken ct)
    {
        var model = await BuildAsync(pondId, ct);

        ViewData["Title"] = "AI Insights";
        ViewData["Subtitle"] = "Advice derived from this farm's own history";
        return View(model);
    }

    /// <summary>Re-runs the models for one pond, or all of them, without reloading the page.</summary>
    [HttpGet]
    public async Task<IActionResult> Panel(int? pondId, CancellationToken ct)
        => PartialView("_Panel", await BuildAsync(pondId, ct));

    private async Task<AiInsightsViewModel> BuildAsync(int? pondId, CancellationToken ct)
    {
        var selected = pondId is > 0 ? pondId : null;

        return new AiInsightsViewModel
        {
            PondId = selected,
            Ponds = await db.Ponds.AsNoTracking()
                .Where(p => p.Status == PondStatus.Active)
                .OrderBy(p => p.Name)
                .Select(p => new SelectOption(p.Id, p.Name))
                .ToListAsync(ct),

            FeedAdvice = await feedAdvisor.RecommendAsync(selected, ct),
            GrowthForecasts = await growthPredictor.ForecastAsync(30, selected, ct),
            FinanceForecast = await financePredictor.ForecastNextMonthAsync(6, ct),
            Anomalies = await anomalyDetector.DetectAsync(ct),
        };
    }
}
