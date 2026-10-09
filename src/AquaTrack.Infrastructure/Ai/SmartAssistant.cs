using System.Text;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using AquaTrack.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Ai;

public record AssistantAnswer(
    string Question,
    string Answer,
    string Intent,
    IReadOnlyList<SeriesPoint> Data,
    IReadOnlyList<string> Suggestions);

public interface ISmartAssistant
{
    /// <summary>AI-5: answers a plain-language question by querying the farm's own data.</summary>
    Task<AssistantAnswer> AskAsync(string question, CancellationToken ct = default);

    IReadOnlyList<string> SampleQuestions { get; }
}

/// <summary>
/// An intent classifier over the farm's vocabulary, in English and Bangla. Each intent maps to a real
/// query, so every answer is grounded in stored data rather than generated text.
/// </summary>
public class SmartAssistant(
    AquaTrackDbContext db,
    IDashboardService dashboard,
    ICashBookService cashBook,
    IFeedAdvisor feedAdvisor,
    IFinancePredictor financePredictor) : ISmartAssistant
{
    public IReadOnlyList<string> SampleQuestions { get; } =
    [
        "Which category cost the most this month?",
        "Which pond has the highest production?",
        "What is today's profit?",
        "How much feed is left in stock?",
        "How much did we sell this month?",
        "Who owes us money?",
        "How much feed should I give today?",
        "What is next month's expected profit?",
    ];

    /// <summary>Keyword sets per intent. Bangla terms are included because farm staff mix both languages.</summary>
    private static readonly (string Intent, string[] Keywords)[] IntentKeywords =
    [
        ("top-expense-category", ["category", "khate", "khat", "most expense", "biggest expense", "highest expense", "beshi khoroch"]),
        ("today-profit", ["today profit", "ajker profit", "today's profit", "profit today", "aj profit"]),
        ("today-summary", ["today", "ajke", "ajker", "aj "]),
        ("month-expense", ["expense", "khoroch", "cost", "spend", "spent"]),
        ("month-income", ["income", "sales", "sold", "revenue", "bikri", "ay"]),
        ("pond-production", ["pond", "pukur", "production", "harvest", "utpadon"]),
        ("feed-stock", ["feed stock", "khaddo", "feed left", "feed remaining", "khabar"]),
        ("feed-advice", ["how much feed", "feed recommendation", "feed today", "koto feed"]),
        ("fish-stock", ["fish", "mach", "stock", "biomass", "quantity"]),
        ("medicine-stock", ["medicine", "osudh", "ousud", "drug"]),
        ("receivables", ["owe", "due", "receivable", "baki", "pawna"]),
        ("forecast", ["next month", "forecast", "predict", "projection", "expected"]),
        ("cash-balance", ["cash", "balance", "hate koto", "in hand"]),
        ("mortality", ["mortality", "death", "died", "mara"]),
    ];

    public async Task<AssistantAnswer> AskAsync(string question, CancellationToken ct = default)
    {
        var text = (question ?? string.Empty).Trim();
        if (text.Length == 0)
            return Empty(text, "Ask me about expenses, sales, ponds, stock, cash or forecasts.");

        var intent = Classify(text);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        return intent switch
        {
            "top-expense-category" => await TopExpenseCategoryAsync(text, monthStart, today, ct),
            "today-profit" or "today-summary" => await TodaySummaryAsync(text, today, ct),
            "month-expense" => await MonthExpenseAsync(text, monthStart, today, ct),
            "month-income" => await MonthIncomeAsync(text, monthStart, today, ct),
            "pond-production" => await PondProductionAsync(text, ct),
            "feed-stock" => await FeedStockAsync(text, ct),
            "feed-advice" => await FeedAdviceAsync(text, ct),
            "fish-stock" => await FishStockAsync(text, today, ct),
            "medicine-stock" => await MedicineStockAsync(text, ct),
            "receivables" => await ReceivablesAsync(text, ct),
            "forecast" => await ForecastAsync(text, ct),
            "cash-balance" => await CashBalanceAsync(text, today, ct),
            "mortality" => await MortalityAsync(text, today, ct),
            _ => Empty(text, "I could not match that to the data I track. Try one of the suggestions below."),
        };
    }

    internal static string Classify(string question)
    {
        var lowered = question.ToLowerInvariant();

        // Longest keyword wins, so "feed stock" beats the generic "stock".
        var best = IntentKeywords
            .SelectMany(entry => entry.Keywords.Select(keyword => (entry.Intent, Keyword: keyword)))
            .Where(pair => lowered.Contains(pair.Keyword, StringComparison.Ordinal))
            .OrderByDescending(pair => pair.Keyword.Length)
            .FirstOrDefault();

        return best.Intent ?? "unknown";
    }

    private AssistantAnswer Empty(string question, string message)
        => new(question, message, "unknown", [], SampleQuestions);

    private async Task<AssistantAnswer> TopExpenseCategoryAsync(
        string q, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var data = await dashboard.GetExpenseByCategoryAsync(from, to, ct);
        if (data.Count == 0)
            return new AssistantAnswer(q, "No expenses have been recorded this month yet.", "top-expense-category", data, SampleQuestions);

        var top = data[0];
        var total = data.Sum(d => d.Value);
        var share = total > 0 ? top.Value / total * 100 : 0;

        var answer = new StringBuilder()
            .Append($"The largest expense this month is {top.Label} at {top.Value:N0}, ")
            .Append($"which is {share:N0}% of the {total:N0} total.");

        if (data.Count > 1)
            answer.Append($" Next is {data[1].Label} at {data[1].Value:N0}.");

        return new AssistantAnswer(q, answer.ToString(), "top-expense-category", data, SampleQuestions);
    }

    private async Task<AssistantAnswer> TodaySummaryAsync(string q, DateOnly today, CancellationToken ct)
    {
        var summary = await cashBook.GetSummaryAsync(today, today, ct);

        var verdict = summary.Net switch
        {
            > 0 => $"a profit of {summary.Net:N0}",
            < 0 => $"a loss of {Math.Abs(summary.Net):N0}",
            _ => "no net movement",
        };

        var answer = $"Today brought in {summary.TotalIn:N0} and paid out {summary.TotalOut:N0}, " +
                     $"so {verdict}. Closing cash balance is {summary.ClosingBalance:N0}.";

        IReadOnlyList<SeriesPoint> data =
        [
            new("Income", summary.TotalIn),
            new("Expense", summary.TotalOut),
            new("Profit", summary.Net),
        ];

        return new AssistantAnswer(q, answer, "today-summary", data, SampleQuestions);
    }

    private async Task<AssistantAnswer> MonthExpenseAsync(string q, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var data = await dashboard.GetExpenseByCategoryAsync(from, to, ct);
        var total = data.Sum(d => d.Value);

        var answer = total == 0
            ? "No expenses recorded for this month yet."
            : $"Total expense for {from:MMMM yyyy} is {total:N0} across {data.Count} categories. " +
              $"The largest is {data[0].Label} at {data[0].Value:N0}.";

        return new AssistantAnswer(q, answer, "month-expense", data, SampleQuestions);
    }

    private async Task<AssistantAnswer> MonthIncomeAsync(string q, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var sales = await db.Sales
            .Where(s => s.SaleDate >= from && s.SaleDate <= to)
            .SumAsync(s => (decimal?)s.GrandTotal, ct) ?? 0m;

        var other = await db.OtherIncomes
            .Where(i => i.IncomeDate >= from && i.IncomeDate <= to)
            .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;

        var byFish = await db.SaleItems
            .Where(i => i.Sale!.SaleDate >= from && i.Sale.SaleDate <= to)
            .GroupBy(i => i.FishType!.Name)
            .Select(g => new SeriesPoint(g.Key, g.Sum(i => i.LineTotal)))
            .OrderByDescending(p => p.Value)
            .ToListAsync(ct);

        var answer = sales + other == 0
            ? "No income recorded for this month yet."
            : $"Income for {from:MMMM yyyy} is {sales + other:N0} - {sales:N0} from fish sales " +
              $"and {other:N0} from other sources." +
              (byFish.Count > 0 ? $" The best seller is {byFish[0].Label} at {byFish[0].Value:N0}." : string.Empty);

        return new AssistantAnswer(q, answer, "month-income", byFish, SampleQuestions);
    }

    private async Task<AssistantAnswer> PondProductionAsync(string q, CancellationToken ct)
    {
        var from = DateOnly.FromDateTime(DateTime.Today.AddYears(-1));
        var to = DateOnly.FromDateTime(DateTime.Today);

        var data = await dashboard.GetProductionByPondAsync(from, to, ct);
        if (data.Count == 0)
            return new AssistantAnswer(q, "No harvest has been recorded in the last 12 months.", "pond-production", data, SampleQuestions);

        var answer = $"{data[0].Label} leads production with {data[0].Value:N1} kg harvested in the last 12 months" +
                     (data.Count > 1 ? $", ahead of {data[1].Label} at {data[1].Value:N1} kg." : ".");

        return new AssistantAnswer(q, answer, "pond-production", data, SampleQuestions);
    }

    private async Task<AssistantAnswer> FeedStockAsync(string q, CancellationToken ct)
    {
        var items = await db.FeedItems
            .Where(f => f.IsActive)
            .Select(f => new { f.Name, f.CurrentStock, f.ReorderLevel, Value = f.CurrentStock * f.AverageUnitCost })
            .OrderByDescending(f => f.CurrentStock)
            .ToListAsync(ct);

        if (items.Count == 0)
            return new AssistantAnswer(q, "No feed items have been set up yet.", "feed-stock", [], SampleQuestions);

        var totalKg = items.Sum(i => i.CurrentStock);
        var totalValue = items.Sum(i => i.Value);
        var low = items.Where(i => i.ReorderLevel > 0 && i.CurrentStock <= i.ReorderLevel).ToList();

        var answer = $"There is {totalKg:N2} of feed in stock worth about {totalValue:N0} across {items.Count} items." +
                     (low.Count > 0
                         ? $" {low.Count} item(s) are at or below reorder level: {string.Join(", ", low.Select(i => i.Name))}."
                         : " Nothing is below reorder level.");

        IReadOnlyList<SeriesPoint> data = [.. items.Select(i => new SeriesPoint(i.Name, i.CurrentStock))];
        return new AssistantAnswer(q, answer, "feed-stock", data, SampleQuestions);
    }

    private async Task<AssistantAnswer> FeedAdviceAsync(string q, CancellationToken ct)
    {
        var advice = await feedAdvisor.RecommendAsync(ct: ct);
        if (advice.Count == 0)
            return new AssistantAnswer(q, "There are no active fish batches to recommend feed for.", "feed-advice", [], SampleQuestions);

        var total = advice.Sum(a => a.RecommendedKgPerDay);
        var answer = $"Recommended feed for today is {total:N2} kg across {advice.Count} active batch(es). " +
                     $"The largest share is {advice[0].PondName} at {advice[0].RecommendedKgPerDay:N2} kg.";

        IReadOnlyList<SeriesPoint> data = [.. advice.Select(a => new SeriesPoint(a.PondName, a.RecommendedKgPerDay))];
        return new AssistantAnswer(q, answer, "feed-advice", data, SampleQuestions);
    }

    private async Task<AssistantAnswer> FishStockAsync(string q, DateOnly today, CancellationToken ct)
    {
        var summary = await dashboard.GetSummaryAsync(today, ct);

        var byPond = await db.FishStocks
            .Where(s => s.Status == StockStatus.Active)
            .GroupBy(s => s.Pond!.Name)
            .Select(g => new SeriesPoint(g.Key, g.Sum(s => s.CurrentQuantity)))
            .OrderByDescending(p => p.Value)
            .ToListAsync(ct);

        var answer = summary.TotalFishCount == 0
            ? "There is no active fish stock recorded."
            : $"There are {summary.TotalFishCount:N0} fish across {summary.ActiveBatches} active batch(es) " +
              $"in {summary.ActivePonds} pond(s), roughly {summary.TotalBiomassKg:N1} kg of biomass." +
              (byPond.Count > 0 ? $" The largest is {byPond[0].Label} with {byPond[0].Value:N0} fish." : string.Empty);

        return new AssistantAnswer(q, answer, "fish-stock", byPond, SampleQuestions);
    }

    private async Task<AssistantAnswer> MedicineStockAsync(string q, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var items = await db.Medicines
            .Where(m => m.IsActive)
            .Select(m => new { m.Name, m.CurrentStock, m.ExpiryDate, Value = m.CurrentStock * m.AverageUnitCost })
            .ToListAsync(ct);

        if (items.Count == 0)
            return new AssistantAnswer(q, "No medicines have been set up yet.", "medicine-stock", [], SampleQuestions);

        var expiring = items.Count(i => i.ExpiryDate != null && i.ExpiryDate <= today.AddDays(30));

        var answer = $"{items.Count} medicine(s) in stock worth about {items.Sum(i => i.Value):N0}." +
                     (expiring > 0 ? $" {expiring} of them expire within 30 days." : " None expire within 30 days.");

        IReadOnlyList<SeriesPoint> data = [.. items.Select(i => new SeriesPoint(i.Name, i.CurrentStock))];
        return new AssistantAnswer(q, answer, "medicine-stock", data, SampleQuestions);
    }

    private async Task<AssistantAnswer> ReceivablesAsync(string q, CancellationToken ct)
    {
        var customers = await db.Customers
            .Where(c => c.ReceivableBalance > 0)
            .OrderByDescending(c => c.ReceivableBalance)
            .Select(c => new SeriesPoint(c.Name, c.ReceivableBalance))
            .ToListAsync(ct);

        var payable = await db.Suppliers.SumAsync(s => (decimal?)s.PayableBalance, ct) ?? 0m;

        var answer = customers.Count == 0
            ? $"No customer dues outstanding. The farm owes suppliers {payable:N0}."
            : $"{customers.Count} customer(s) owe a total of {customers.Sum(c => c.Value):N0}. " +
              $"The largest is {customers[0].Label} at {customers[0].Value:N0}. " +
              $"The farm owes suppliers {payable:N0}.";

        return new AssistantAnswer(q, answer, "receivables", customers, SampleQuestions);
    }

    private async Task<AssistantAnswer> ForecastAsync(string q, CancellationToken ct)
    {
        var forecast = await financePredictor.ForecastNextMonthAsync(ct: ct);

        var answer = $"For {forecast.Month:MMMM yyyy} the projection is {forecast.PredictedIncome:N0} income " +
                     $"against {forecast.PredictedExpense:N0} expense, a profit of {forecast.PredictedProfit:N0}. " +
                     $"Confidence {forecast.Confidence:P0}. {forecast.Rationale}";

        IReadOnlyList<SeriesPoint> data =
        [
            .. forecast.History.Select(h => new SeriesPoint(h.Month.ToString("MMM yy"), h.Profit)),
            new($"{forecast.Month:MMM yy} (est)", forecast.PredictedProfit),
        ];

        return new AssistantAnswer(q, answer, "forecast", data, SampleQuestions);
    }

    private async Task<AssistantAnswer> CashBalanceAsync(string q, DateOnly today, CancellationToken ct)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var summary = await cashBook.GetSummaryAsync(monthStart, today, ct);

        var answer = $"Cash in hand is {summary.ClosingBalance:N0}. " +
                     $"This month opened at {summary.OpeningBalance:N0}, took in {summary.TotalIn:N0} " +
                     $"and paid out {summary.TotalOut:N0}.";

        IReadOnlyList<SeriesPoint> data =
        [
            new("Opening", summary.OpeningBalance),
            new("In", summary.TotalIn),
            new("Out", summary.TotalOut),
            new("Closing", summary.ClosingBalance),
        ];

        return new AssistantAnswer(q, answer, "cash-balance", data, SampleQuestions);
    }

    private async Task<AssistantAnswer> MortalityAsync(string q, DateOnly today, CancellationToken ct)
    {
        var from = today.AddDays(-30);

        var byPond = await db.FishMortalities
            .Where(m => m.RecordDate >= from && m.RecordDate <= today)
            .GroupBy(m => m.FishStock!.Pond!.Name)
            .Select(g => new SeriesPoint(g.Key, g.Sum(m => m.Quantity)))
            .OrderByDescending(p => p.Value)
            .ToListAsync(ct);

        var total = byPond.Sum(p => p.Value);

        var answer = total == 0
            ? "No fish mortality recorded in the last 30 days."
            : $"{total:N0} fish died in the last 30 days. " +
              $"The worst affected is {byPond[0].Label} with {byPond[0].Value:N0}.";

        return new AssistantAnswer(q, answer, "mortality", byPond, SampleQuestions);
    }
}
