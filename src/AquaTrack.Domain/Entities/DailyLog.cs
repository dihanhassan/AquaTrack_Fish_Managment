using AquaTrack.Domain.Common;

namespace AquaTrack.Domain.Entities;

/// <summary>
/// Header for the Daily Input screen. One log per pond per day; the child records it creates
/// (feed usage, medicine usage, mortality, growth, sale, expense) point back at it.
/// </summary>
public class DailyLog : BaseEntity
{
    public DateOnly LogDate { get; set; }

    public int PondId { get; set; }
    public Pond? Pond { get; set; }

    public string? Weather { get; set; }
    public string? Observations { get; set; }
    public bool IsLocked { get; set; }

    public decimal TotalFeedKg { get; set; }
    public decimal TotalFeedCost { get; set; }
    public decimal TotalMedicineCost { get; set; }
    public int TotalMortality { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalOtherIncome { get; set; }
    public decimal TotalExpense { get; set; }

    public decimal NetResult => TotalSales + TotalOtherIncome - TotalExpense;
}
