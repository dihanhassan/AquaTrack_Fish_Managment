using AquaTrack.Domain.Enums;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Application.Reporting;

public enum ReportKind
{
    FishStock, FishGrowth, FishMortality, Production,
    FeedPurchase, FeedUsage, FeedStock,
    MedicineStock, MedicineUsage,
    Sales, SalesByCustomer, SalesByFish,
    Purchases, Expenses, ProfitLoss, CashFlow,
}

public interface IReportService
{
    Task<ReportTable> BuildAsync(ReportKind kind, DateOnly from, DateOnly to, CancellationToken ct = default);
}

/// <summary>Turns each report definition into a <see cref="ReportTable"/> the exporter can render.</summary>
public class ReportService(IApplicationDbContext db, ICashBookService cashBook) : IReportService
{
    private const string Money = "N2";

    public Task<ReportTable> BuildAsync(ReportKind kind, DateOnly from, DateOnly to, CancellationToken ct = default)
        => kind switch
        {
            ReportKind.FishStock => FishStockAsync(from, to, ct),
            ReportKind.FishGrowth => FishGrowthAsync(from, to, ct),
            ReportKind.FishMortality => FishMortalityAsync(from, to, ct),
            ReportKind.Production => ProductionAsync(from, to, ct),
            ReportKind.FeedPurchase => FeedPurchaseAsync(from, to, ct),
            ReportKind.FeedUsage => FeedUsageAsync(from, to, ct),
            ReportKind.FeedStock => FeedStockAsync(from, to, ct),
            ReportKind.MedicineStock => MedicineStockAsync(from, to, ct),
            ReportKind.MedicineUsage => MedicineUsageAsync(from, to, ct),
            ReportKind.Sales => SalesAsync(from, to, ct),
            ReportKind.SalesByCustomer => SalesByCustomerAsync(from, to, ct),
            ReportKind.SalesByFish => SalesByFishAsync(from, to, ct),
            ReportKind.Purchases => PurchasesAsync(from, to, ct),
            ReportKind.Expenses => ExpensesAsync(from, to, ct),
            ReportKind.ProfitLoss => ProfitLossAsync(from, to, ct),
            ReportKind.CashFlow => CashFlowAsync(from, to, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown report."),
        };

    private async Task<ReportTable> FishStockAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.FishStocks
            .Include(s => s.Pond).Include(s => s.FishType)
            .Where(s => s.StockDate <= to)
            .OrderBy(s => s.Pond!.Name).ThenBy(s => s.StockDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return new ReportTable
        {
            Title = "Pond-wise Fish Stock",
            From = from,
            To = to,
            Columns =
            [
                new("Pond", ReportAlign.Left, 1.2f), new("Species"), new("Batch"),
                new("Stocked On", ReportAlign.Center), new("Initial", ReportAlign.Right),
                new("Current", ReportAlign.Right), new("Mortality", ReportAlign.Right),
                new("Avg Wt (g)", ReportAlign.Right), new("Biomass (kg)", ReportAlign.Right),
                new("Status", ReportAlign.Center),
            ],
            Rows =
            [
                .. rows.Select(IReadOnlyList<string> (s) =>
                [
                    s.Pond?.Name ?? "-",
                    s.FishType?.Name ?? "-",
                    s.BatchCode ?? $"#{s.Id}",
                    s.StockDate.ToString("dd MMM yyyy"),
                    s.InitialQuantity.ToString("N0"),
                    s.CurrentQuantity.ToString("N0"),
                    s.TotalMortality.ToString("N0"),
                    s.CurrentAvgWeightGram.ToString("N0"),
                    s.BiomassKg.ToString(Money),
                    s.Status.ToString(),
                ]),
            ],
            Summary =
            [
                ("Batches", rows.Count.ToString("N0")),
                ("Live fish", rows.Sum(s => s.CurrentQuantity).ToString("N0")),
                ("Total biomass", $"{rows.Sum(s => s.BiomassKg):N2} kg"),
                ("Stocking cost", rows.Sum(s => s.TotalStockingCost).ToString(Money)),
            ],
        };
    }

    private async Task<ReportTable> FishGrowthAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.FishGrowths
            .Include(g => g.FishStock).ThenInclude(s => s!.Pond)
            .Include(g => g.FishStock).ThenInclude(s => s!.FishType)
            .Where(g => g.RecordDate >= from && g.RecordDate <= to)
            .OrderBy(g => g.RecordDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return new ReportTable
        {
            Title = "Fish Growth Record",
            From = from,
            To = to,
            Columns =
            [
                new("Date", ReportAlign.Center), new("Pond"), new("Species"),
                new("Sample Size", ReportAlign.Right), new("Avg Weight (g)", ReportAlign.Right),
                new("Avg Length (cm)", ReportAlign.Right), new("Notes", ReportAlign.Left, 1.6f),
            ],
            Rows =
            [
                .. rows.Select(IReadOnlyList<string> (g) =>
                [
                    g.RecordDate.ToString("dd MMM yyyy"),
                    g.FishStock?.Pond?.Name ?? "-",
                    g.FishStock?.FishType?.Name ?? "-",
                    g.SampleSize.ToString("N0"),
                    g.AvgWeightGram.ToString("N1"),
                    g.AvgLengthCm?.ToString("N1") ?? "-",
                    g.Notes ?? string.Empty,
                ]),
            ],
            Summary = [("Samples taken", rows.Count.ToString("N0"))],
        };
    }

    private async Task<ReportTable> FishMortalityAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.FishMortalities
            .Include(m => m.FishStock).ThenInclude(s => s!.Pond)
            .Include(m => m.FishStock).ThenInclude(s => s!.FishType)
            .Where(m => m.RecordDate >= from && m.RecordDate <= to)
            .OrderBy(m => m.RecordDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return new ReportTable
        {
            Title = "Fish Mortality Report",
            From = from,
            To = to,
            Columns =
            [
                new("Date", ReportAlign.Center), new("Pond"), new("Species"),
                new("Quantity", ReportAlign.Right), new("Cause"),
                new("Est. Loss", ReportAlign.Right), new("Notes", ReportAlign.Left, 1.4f),
            ],
            Rows =
            [
                .. rows.Select(IReadOnlyList<string> (m) =>
                [
                    m.RecordDate.ToString("dd MMM yyyy"),
                    m.FishStock?.Pond?.Name ?? "-",
                    m.FishStock?.FishType?.Name ?? "-",
                    m.Quantity.ToString("N0"),
                    m.Cause.ToString(),
                    m.EstimatedLoss.ToString(Money),
                    m.Notes ?? string.Empty,
                ]),
            ],
            Summary =
            [
                ("Fish lost", rows.Sum(m => m.Quantity).ToString("N0")),
                ("Estimated value", rows.Sum(m => m.EstimatedLoss).ToString(Money)),
            ],
            TotalsRow = ["Total", string.Empty, string.Empty,
                rows.Sum(m => m.Quantity).ToString("N0"), string.Empty,
                rows.Sum(m => m.EstimatedLoss).ToString(Money), string.Empty],
        };
    }

    private async Task<ReportTable> ProductionAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.Harvests
            .Include(h => h.FishStock).ThenInclude(s => s!.Pond)
            .Include(h => h.FishStock).ThenInclude(s => s!.FishType)
            .Where(h => h.HarvestDate >= from && h.HarvestDate <= to)
            .OrderBy(h => h.HarvestDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return new ReportTable
        {
            Title = "Production (Harvest) Report",
            From = from,
            To = to,
            Columns =
            [
                new("Date", ReportAlign.Center), new("Pond"), new("Species"),
                new("Pieces", ReportAlign.Right), new("Weight (kg)", ReportAlign.Right),
                new("Avg Wt (g)", ReportAlign.Right), new("Est. Value", ReportAlign.Right),
            ],
            Rows =
            [
                .. rows.Select(IReadOnlyList<string> (h) =>
                [
                    h.HarvestDate.ToString("dd MMM yyyy"),
                    h.FishStock?.Pond?.Name ?? "-",
                    h.FishStock?.FishType?.Name ?? "-",
                    h.Quantity.ToString("N0"),
                    h.TotalWeightKg.ToString("N2"),
                    h.AvgWeightGram.ToString("N0"),
                    (h.TotalWeightKg * (h.ExpectedUnitPrice ?? 0m)).ToString(Money),
                ]),
            ],
            Summary =
            [
                ("Total harvested", $"{rows.Sum(h => h.TotalWeightKg):N2} kg"),
                ("Pieces", rows.Sum(h => h.Quantity).ToString("N0")),
            ],
            TotalsRow = ["Total", string.Empty, string.Empty,
                rows.Sum(h => h.Quantity).ToString("N0"),
                rows.Sum(h => h.TotalWeightKg).ToString("N2"), string.Empty,
                rows.Sum(h => h.TotalWeightKg * (h.ExpectedUnitPrice ?? 0m)).ToString(Money)],
        };
    }

    private async Task<ReportTable> FeedPurchaseAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.FeedPurchases
            .Include(p => p.FeedItem).Include(p => p.Supplier)
            .Where(p => p.PurchaseDate >= from && p.PurchaseDate <= to)
            .OrderBy(p => p.PurchaseDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return new ReportTable
        {
            Title = "Feed Purchase Report",
            From = from,
            To = to,
            Columns =
            [
                new("Date", ReportAlign.Center), new("Feed Item", ReportAlign.Left, 1.3f), new("Supplier"),
                new("Quantity", ReportAlign.Right), new("Unit Price", ReportAlign.Right),
                new("Total", ReportAlign.Right), new("Paid", ReportAlign.Right), new("Due", ReportAlign.Right),
            ],
            Rows =
            [
                .. rows.Select(IReadOnlyList<string> (p) =>
                [
                    p.PurchaseDate.ToString("dd MMM yyyy"),
                    p.FeedItem?.Name ?? "-",
                    p.Supplier?.Name ?? "-",
                    p.Quantity.ToString("N2"),
                    p.UnitPrice.ToString(Money),
                    p.TotalPrice.ToString(Money),
                    p.PaidAmount.ToString(Money),
                    p.DueAmount.ToString(Money),
                ]),
            ],
            Summary =
            [
                ("Quantity", rows.Sum(p => p.Quantity).ToString("N2")),
                ("Total cost", rows.Sum(p => p.TotalPrice).ToString(Money)),
                ("Outstanding", rows.Sum(p => p.DueAmount).ToString(Money)),
            ],
            TotalsRow = ["Total", string.Empty, string.Empty,
                rows.Sum(p => p.Quantity).ToString("N2"), string.Empty,
                rows.Sum(p => p.TotalPrice).ToString(Money),
                rows.Sum(p => p.PaidAmount).ToString(Money),
                rows.Sum(p => p.DueAmount).ToString(Money)],
        };
    }

    private async Task<ReportTable> FeedUsageAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.FeedUsages
            .Include(u => u.FeedItem).Include(u => u.Pond)
            .Where(u => u.UsageDate >= from && u.UsageDate <= to)
            .OrderBy(u => u.UsageDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return new ReportTable
        {
            Title = "Feed Usage Report",
            From = from,
            To = to,
            Columns =
            [
                new("Date", ReportAlign.Center), new("Pond"), new("Feed Item", ReportAlign.Left, 1.3f),
                new("Quantity", ReportAlign.Right), new("Unit Cost", ReportAlign.Right),
                new("Total Cost", ReportAlign.Right),
            ],
            Rows =
            [
                .. rows.Select(IReadOnlyList<string> (u) =>
                [
                    u.UsageDate.ToString("dd MMM yyyy"),
                    u.Pond?.Name ?? "-",
                    u.FeedItem?.Name ?? "-",
                    u.Quantity.ToString("N2"),
                    u.UnitCost.ToString(Money),
                    u.TotalCost.ToString(Money),
                ]),
            ],
            Summary =
            [
                ("Feed used", $"{rows.Sum(u => u.Quantity):N2} kg"),
                ("Feed cost", rows.Sum(u => u.TotalCost).ToString(Money)),
            ],
            TotalsRow = ["Total", string.Empty, string.Empty,
                rows.Sum(u => u.Quantity).ToString("N2"), string.Empty,
                rows.Sum(u => u.TotalCost).ToString(Money)],
        };
    }

    private async Task<ReportTable> FeedStockAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.FeedItems
            .Include(f => f.Supplier)
            .Where(f => f.IsActive)
            .OrderBy(f => f.Name)
            .AsNoTracking()
            .ToListAsync(ct);

        return new ReportTable
        {
            Title = "Feed Stock Report",
            Subtitle = "Current stock position",
            To = to,
            Columns =
            [
                new("Feed Item", ReportAlign.Left, 1.4f), new("Brand"), new("Type"),
                new("Stock", ReportAlign.Right), new("Reorder Level", ReportAlign.Right),
                new("Avg Cost", ReportAlign.Right), new("Stock Value", ReportAlign.Right),
                new("Status", ReportAlign.Center),
            ],
            Rows =
            [
                .. rows.Select(IReadOnlyList<string> (f) =>
                [
                    f.Name,
                    f.Brand ?? "-",
                    f.FeedType.ToString(),
                    f.CurrentStock.ToString("N2"),
                    f.ReorderLevel.ToString("N2"),
                    f.AverageUnitCost.ToString(Money),
                    f.StockValue.ToString(Money),
                    f.IsLowStock ? "LOW" : "OK",
                ]),
            ],
            Summary =
            [
                ("Items", rows.Count.ToString("N0")),
                ("Total stock", rows.Sum(f => f.CurrentStock).ToString("N2")),
                ("Stock value", rows.Sum(f => f.StockValue).ToString(Money)),
                ("Low stock items", rows.Count(f => f.IsLowStock).ToString("N0")),
            ],
        };
    }

    private async Task<ReportTable> MedicineStockAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.Medicines
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .AsNoTracking()
            .ToListAsync(ct);

        return new ReportTable
        {
            Title = "Medicine Stock and Expiry Report",
            To = to,
            Columns =
            [
                new("Medicine", ReportAlign.Left, 1.4f), new("Type"), new("Stock", ReportAlign.Right),
                new("Reorder Level", ReportAlign.Right), new("Avg Cost", ReportAlign.Right),
                new("Stock Value", ReportAlign.Right), new("Expiry", ReportAlign.Center),
                new("Status", ReportAlign.Center),
            ],
            Rows =
            [
                .. rows.Select(IReadOnlyList<string> (m) =>
                [
                    m.Name,
                    m.MedicineType.ToString(),
                    m.CurrentStock.ToString("N2"),
                    m.ReorderLevel.ToString("N2"),
                    m.AverageUnitCost.ToString(Money),
                    m.StockValue.ToString(Money),
                    m.ExpiryDate?.ToString("dd MMM yyyy") ?? "-",
                    m.IsExpired ? "EXPIRED" : m.IsExpiringSoon ? "EXPIRING" : m.IsLowStock ? "LOW" : "OK",
                ]),
            ],
            Summary =
            [
                ("Items", rows.Count.ToString("N0")),
                ("Stock value", rows.Sum(m => m.StockValue).ToString(Money)),
                ("Expired", rows.Count(m => m.IsExpired).ToString("N0")),
                ("Expiring soon", rows.Count(m => m.IsExpiringSoon).ToString("N0")),
            ],
        };
    }

    private async Task<ReportTable> MedicineUsageAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.MedicineUsages
            .Include(u => u.Medicine).Include(u => u.Pond)
            .Where(u => u.UsageDate >= from && u.UsageDate <= to)
            .OrderBy(u => u.UsageDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return new ReportTable
        {
            Title = "Medicine Usage Report",
            From = from,
            To = to,
            Columns =
            [
                new("Date", ReportAlign.Center), new("Pond"), new("Medicine", ReportAlign.Left, 1.3f),
                new("Quantity", ReportAlign.Right), new("Total Cost", ReportAlign.Right),
                new("Purpose", ReportAlign.Left, 1.4f),
            ],
            Rows =
            [
                .. rows.Select(IReadOnlyList<string> (u) =>
                [
                    u.UsageDate.ToString("dd MMM yyyy"),
                    u.Pond?.Name ?? "-",
                    u.Medicine?.Name ?? "-",
                    u.Quantity.ToString("N2"),
                    u.TotalCost.ToString(Money),
                    u.Purpose ?? string.Empty,
                ]),
            ],
            Summary = [("Treatment cost", rows.Sum(u => u.TotalCost).ToString(Money))],
            TotalsRow = ["Total", string.Empty, string.Empty,
                rows.Sum(u => u.Quantity).ToString("N2"),
                rows.Sum(u => u.TotalCost).ToString(Money), string.Empty],
        };
    }

    private async Task<ReportTable> SalesAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.Sales
            .Include(s => s.Customer).Include(s => s.Items).ThenInclude(i => i.FishType)
            .Where(s => s.SaleDate >= from && s.SaleDate <= to)
            .OrderBy(s => s.SaleDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return new ReportTable
        {
            Title = "Sales Report",
            From = from,
            To = to,
            Columns =
            [
                new("Date", ReportAlign.Center), new("Invoice"), new("Customer", ReportAlign.Left, 1.3f),
                new("Items", ReportAlign.Left, 1.5f), new("Qty (kg)", ReportAlign.Right),
                new("Total", ReportAlign.Right), new("Paid", ReportAlign.Right), new("Due", ReportAlign.Right),
            ],
            Rows =
            [
                .. rows.Select(IReadOnlyList<string> (s) =>
                [
                    s.SaleDate.ToString("dd MMM yyyy"),
                    s.InvoiceNo,
                    s.Customer?.Name ?? "Walk-in",
                    string.Join(", ", s.Items.Select(i => i.FishType?.Name ?? "-")),
                    s.Items.Sum(i => i.QuantityKg).ToString("N2"),
                    s.GrandTotal.ToString(Money),
                    s.PaidAmount.ToString(Money),
                    s.DueAmount.ToString(Money),
                ]),
            ],
            Summary =
            [
                ("Invoices", rows.Count.ToString("N0")),
                ("Quantity sold", $"{rows.Sum(s => s.Items.Sum(i => i.QuantityKg)):N2} kg"),
                ("Total sales", rows.Sum(s => s.GrandTotal).ToString(Money)),
                ("Outstanding", rows.Sum(s => s.DueAmount).ToString(Money)),
            ],
            TotalsRow = ["Total", string.Empty, string.Empty, string.Empty,
                rows.Sum(s => s.Items.Sum(i => i.QuantityKg)).ToString("N2"),
                rows.Sum(s => s.GrandTotal).ToString(Money),
                rows.Sum(s => s.PaidAmount).ToString(Money),
                rows.Sum(s => s.DueAmount).ToString(Money)],
        };
    }

    private async Task<ReportTable> SalesByCustomerAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.Sales
            .Where(s => s.SaleDate >= from && s.SaleDate <= to)
            .GroupBy(s => new { Name = s.Customer != null ? s.Customer.Name : "Walk-in" })
            .Select(g => new
            {
                g.Key.Name,
                Invoices = g.Count(),
                Total = g.Sum(s => s.GrandTotal),
                Paid = g.Sum(s => s.PaidAmount),
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync(ct);

        return new ReportTable
        {
            Title = "Customer-wise Sales Report",
            From = from,
            To = to,
            Columns =
            [
                new("Customer", ReportAlign.Left, 2f), new("Invoices", ReportAlign.Right),
                new("Total Sales", ReportAlign.Right), new("Paid", ReportAlign.Right),
                new("Due", ReportAlign.Right),
            ],
            Rows =
            [
                .. rows.Select(IReadOnlyList<string> (r) =>
                [
                    r.Name,
                    r.Invoices.ToString("N0"),
                    r.Total.ToString(Money),
                    r.Paid.ToString(Money),
                    (r.Total - r.Paid).ToString(Money),
                ]),
            ],
            TotalsRow = ["Total", rows.Sum(r => r.Invoices).ToString("N0"),
                rows.Sum(r => r.Total).ToString(Money),
                rows.Sum(r => r.Paid).ToString(Money),
                rows.Sum(r => r.Total - r.Paid).ToString(Money)],
        };
    }

    private async Task<ReportTable> SalesByFishAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.SaleItems
            .Where(i => i.Sale!.SaleDate >= from && i.Sale.SaleDate <= to)
            .GroupBy(i => i.FishType!.Name)
            .Select(g => new
            {
                Name = g.Key,
                Quantity = g.Sum(i => i.QuantityKg),
                Total = g.Sum(i => i.LineTotal),
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync(ct);

        return new ReportTable
        {
            Title = "Fish-wise Sales Report",
            From = from,
            To = to,
            Columns =
            [
                new("Species", ReportAlign.Left, 2f), new("Quantity (kg)", ReportAlign.Right),
                new("Revenue", ReportAlign.Right), new("Avg Price/kg", ReportAlign.Right),
            ],
            Rows =
            [
                .. rows.Select(IReadOnlyList<string> (r) =>
                [
                    r.Name,
                    r.Quantity.ToString("N2"),
                    r.Total.ToString(Money),
                    (r.Quantity > 0 ? r.Total / r.Quantity : 0m).ToString(Money),
                ]),
            ],
            TotalsRow = ["Total", rows.Sum(r => r.Quantity).ToString("N2"),
                rows.Sum(r => r.Total).ToString(Money), string.Empty],
        };
    }

    private async Task<ReportTable> PurchasesAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.Purchases
            .Include(p => p.Supplier).Include(p => p.Items)
            .Where(p => p.PurchaseDate >= from && p.PurchaseDate <= to)
            .OrderBy(p => p.PurchaseDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return new ReportTable
        {
            Title = "Purchase Report",
            From = from,
            To = to,
            Columns =
            [
                new("Date", ReportAlign.Center), new("Purchase No"), new("Supplier", ReportAlign.Left, 1.3f),
                new("Lines", ReportAlign.Right), new("Total", ReportAlign.Right),
                new("Paid", ReportAlign.Right), new("Due", ReportAlign.Right),
            ],
            Rows =
            [
                .. rows.Select(IReadOnlyList<string> (p) =>
                [
                    p.PurchaseDate.ToString("dd MMM yyyy"),
                    p.PurchaseNo,
                    p.Supplier?.Name ?? "-",
                    p.Items.Count.ToString("N0"),
                    p.GrandTotal.ToString(Money),
                    p.PaidAmount.ToString(Money),
                    p.DueAmount.ToString(Money),
                ]),
            ],
            Summary =
            [
                ("Purchases", rows.Count.ToString("N0")),
                ("Total value", rows.Sum(p => p.GrandTotal).ToString(Money)),
                ("Outstanding", rows.Sum(p => p.DueAmount).ToString(Money)),
            ],
            TotalsRow = ["Total", string.Empty, string.Empty, string.Empty,
                rows.Sum(p => p.GrandTotal).ToString(Money),
                rows.Sum(p => p.PaidAmount).ToString(Money),
                rows.Sum(p => p.DueAmount).ToString(Money)],
        };
    }

    private async Task<ReportTable> ExpensesAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.Expenses
            .Include(e => e.ExpenseCategory).Include(e => e.Pond)
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .OrderBy(e => e.ExpenseDate)
            .AsNoTracking()
            .ToListAsync(ct);

        var byCategory = rows
            .GroupBy(e => e.ExpenseCategory?.Name ?? "Other")
            .OrderByDescending(g => g.Sum(e => e.Amount))
            .ToList();

        return new ReportTable
        {
            Title = "Expense Report",
            Subtitle = byCategory.Count > 0
                ? $"Largest category: {byCategory[0].Key} ({byCategory[0].Sum(e => e.Amount):N2})"
                : null,
            From = from,
            To = to,
            Columns =
            [
                new("Date", ReportAlign.Center), new("Category"), new("Pond"),
                new("Description", ReportAlign.Left, 2f), new("Method", ReportAlign.Center),
                new("Amount", ReportAlign.Right),
            ],
            Rows =
            [
                .. rows.Select(IReadOnlyList<string> (e) =>
                [
                    e.ExpenseDate.ToString("dd MMM yyyy"),
                    e.ExpenseCategory?.Name ?? "-",
                    e.Pond?.Name ?? "-",
                    e.Description ?? string.Empty,
                    e.PaymentMethod.ToString(),
                    e.Amount.ToString(Money),
                ]),
            ],
            Summary =
            [
                .. byCategory.Take(4).Select(g => (g.Key, g.Sum(e => e.Amount).ToString(Money))),
            ],
            TotalsRow = ["Total", string.Empty, string.Empty, string.Empty, string.Empty,
                rows.Sum(e => e.Amount).ToString(Money)],
        };
    }

    private async Task<ReportTable> ProfitLossAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var salesTotal = await db.Sales
            .Where(s => s.SaleDate >= from && s.SaleDate <= to)
            .SumAsync(s => (decimal?)s.GrandTotal, ct) ?? 0m;

        var otherIncome = await db.OtherIncomes
            .Where(i => i.IncomeDate >= from && i.IncomeDate <= to)
            .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;

        var expenseByCategory = await db.Expenses
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .GroupBy(e => e.ExpenseCategory!.Name)
            .Select(g => new { Category = g.Key, Amount = g.Sum(e => e.Amount) })
            .OrderByDescending(x => x.Amount)
            .ToListAsync(ct);

        var totalIncome = salesTotal + otherIncome;
        var totalExpense = expenseByCategory.Sum(e => e.Amount);
        var profit = totalIncome - totalExpense;

        // Built as an explicit list because a P&L is a sequence of sections, not a uniform grid.
        var lines = new List<IReadOnlyList<string>>
        {
            new[] { "INCOME", string.Empty, string.Empty },
            new[] { string.Empty, "Fish sales", salesTotal.ToString(Money) },
            new[] { string.Empty, "Other income", otherIncome.ToString(Money) },
            new[] { string.Empty, "Total income", totalIncome.ToString(Money) },
            new[] { string.Empty, string.Empty, string.Empty },
            new[] { "EXPENSE", string.Empty, string.Empty },
        };

        lines.AddRange(expenseByCategory.Select(IReadOnlyList<string> (e) =>
            new[] { string.Empty, e.Category, e.Amount.ToString(Money) }));

        lines.Add(new[] { string.Empty, "Total expense", totalExpense.ToString(Money) });

        return new ReportTable
        {
            Title = "Profit and Loss Statement",
            From = from,
            To = to,
            Columns =
            [
                new("Section", ReportAlign.Left, 0.8f),
                new("Particulars", ReportAlign.Left, 2.2f),
                new("Amount", ReportAlign.Right),
            ],
            Rows = lines,
            Summary =
            [
                ("Total income", totalIncome.ToString(Money)),
                ("Total expense", totalExpense.ToString(Money)),
                (profit >= 0 ? "Net profit" : "Net loss", Math.Abs(profit).ToString(Money)),
                ("Margin", totalIncome > 0 ? $"{profit / totalIncome * 100:N1}%" : "-"),
            ],
            TotalsRow = [profit >= 0 ? "NET PROFIT" : "NET LOSS", string.Empty, profit.ToString(Money)],
        };
    }

    private async Task<ReportTable> CashFlowAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var ledger = await cashBook.GetLedgerAsync(from, to, ct);
        var summary = await cashBook.GetSummaryAsync(from, to, ct);

        return new ReportTable
        {
            Title = "Cash Flow Statement",
            Subtitle = "Opening Balance + Cash In - Cash Out = Closing Balance",
            From = from,
            To = to,
            Columns =
            [
                new("Date", ReportAlign.Center), new("Category"), new("Description", ReportAlign.Left, 2f),
                new("Method", ReportAlign.Center), new("Cash In", ReportAlign.Right),
                new("Cash Out", ReportAlign.Right), new("Balance", ReportAlign.Right),
            ],
            Rows =
            [
                .. ledger.Select(IReadOnlyList<string> (row) =>
                [
                    row.Transaction.TransactionDate.ToString("dd MMM yyyy"),
                    row.Transaction.Category,
                    row.Transaction.Description ?? string.Empty,
                    row.Transaction.PaymentMethod.ToString(),
                    row.Transaction.Direction == CashDirection.In ? row.Transaction.Amount.ToString(Money) : string.Empty,
                    row.Transaction.Direction == CashDirection.Out ? row.Transaction.Amount.ToString(Money) : string.Empty,
                    row.RunningBalance.ToString(Money),
                ]),
            ],
            Summary =
            [
                ("Opening balance", summary.OpeningBalance.ToString(Money)),
                ("Cash in", summary.TotalIn.ToString(Money)),
                ("Cash out", summary.TotalOut.ToString(Money)),
                ("Closing balance", summary.ClosingBalance.ToString(Money)),
            ],
            TotalsRow = ["Total", string.Empty, string.Empty, string.Empty,
                summary.TotalIn.ToString(Money),
                summary.TotalOut.ToString(Money),
                summary.ClosingBalance.ToString(Money)],
        };
    }
}
