using AquaTrack.Application.Reporting;
using AquaTrack.Application.Services;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// Report viewing and export. The application layer decides what a report contains; this
/// controller only picks the date range and the output format.
/// </summary>
[Authorize(Policy = Policies.ManageFarm)]
public class ReportsController(
    IReportService reports,
    IReportExporter exporter,
    ISettingsService settings) : AppController
{
    /// <summary>The catalogue, grouped the way the requirements document describes them.</summary>
    private static readonly ReportGroup[] Catalogue =
    [
        new("Fish", "bi-water",
        [
            new(ReportKind.FishStock, "Pond-wise Fish Stock", "Live batches, biomass and stocking cost"),
            new(ReportKind.FishGrowth, "Fish Growth", "Every weight sample taken"),
            new(ReportKind.FishMortality, "Fish Mortality", "Deaths, causes and the value lost"),
            new(ReportKind.Production, "Production (Harvest)", "What came out of each pond"),
        ]),
        new("Feed", "bi-basket",
        [
            new(ReportKind.FeedPurchase, "Feed Purchase", "What was bought, from whom, and what is owed"),
            new(ReportKind.FeedUsage, "Feed Usage", "Feed issued to each pond and its cost"),
            new(ReportKind.FeedStock, "Feed Stock", "Current levels, value and low-stock items"),
        ]),
        new("Medicine", "bi-capsule",
        [
            new(ReportKind.MedicineStock, "Medicine Stock and Expiry", "Levels, value and what is expiring"),
            new(ReportKind.MedicineUsage, "Medicine Usage", "Treatments by pond and their cost"),
        ]),
        new("Sales", "bi-receipt",
        [
            new(ReportKind.Sales, "Sales", "Every invoice with paid and outstanding amounts"),
            new(ReportKind.SalesByCustomer, "Customer-wise Sales", "Who buys most, and who owes"),
            new(ReportKind.SalesByFish, "Fish-wise Sales", "Revenue and average price per species"),
        ]),
        new("Finance", "bi-cash-stack",
        [
            new(ReportKind.Purchases, "Purchases", "Every purchase document and supplier dues"),
            new(ReportKind.Expenses, "Expenses", "Costs by category over the period"),
            new(ReportKind.ProfitLoss, "Profit and Loss", "Income against expense, with the margin"),
            new(ReportKind.CashFlow, "Cash Flow", "The cash book with a running balance"),
        ]),
    ];

    public IActionResult Index()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        ViewData["Title"] = "Reports";
        ViewData["Subtitle"] = "Every report exports to PDF, Excel or CSV";

        return View(new ReportCatalogueViewModel
        {
            Groups = Catalogue,
            From = new DateOnly(today.Year, today.Month, 1),
            To = today,
        });
    }

    public async Task<IActionResult> Report(
        ReportKind kind, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (start, end) = ResolveRange(from, to);
        var table = await reports.BuildAsync(kind, start, end, ct);

        ViewData["Title"] = table.Title;
        ViewData["Subtitle"] = table.PeriodLabel;

        return View(new ReportViewModel
        {
            Kind = kind,
            Table = table,
            From = start,
            To = end,
        });
    }

    /// <summary>Re-renders just the grid when the date range changes.</summary>
    [HttpGet]
    public async Task<IActionResult> Preview(
        ReportKind kind, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (start, end) = ResolveRange(from, to);

        return PartialView("_Preview", new ReportViewModel
        {
            Kind = kind,
            Table = await reports.BuildAsync(kind, start, end, ct),
            From = start,
            To = end,
        });
    }

    public async Task<IActionResult> Export(
        ReportKind kind, ReportFormat format, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (start, end) = ResolveRange(from, to);

        var table = await reports.BuildAsync(kind, start, end, ct);
        var farmName = await settings.GetAsync(SettingKeys.FarmName, "AquaTrack Fish Farm", ct);

        var bytes = format switch
        {
            ReportFormat.Pdf => exporter.ToPdf(table, farmName),
            ReportFormat.Excel => exporter.ToExcel(table, farmName),
            _ => exporter.ToCsv(table),
        };

        // A dated filename keeps downloads distinguishable once a few are saved together.
        var slug = new string(table.Title.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray())
            .Replace(' ', '-')
            .ToLowerInvariant();

        var fileName = $"{slug}-{start:yyyyMMdd}-{end:yyyyMMdd}.{format.Extension()}";

        return File(bytes, format.ContentType(), fileName);
    }

    /// <summary>Defaults to the current month, and tolerates a reversed range.</summary>
    private static (DateOnly From, DateOnly To) ResolveRange(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;

        return start > end ? (end, start) : (start, end);
    }
}
