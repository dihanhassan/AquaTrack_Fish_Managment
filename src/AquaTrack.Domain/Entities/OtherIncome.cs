using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

/// <summary>Income that is not a fish sale — pond lease, scrap, subsidy, and so on.</summary>
public class OtherIncome : BaseEntity
{
    public DateOnly IncomeDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Description { get; set; }

    /// <summary>Set when this row was created from the Daily Input screen.</summary>
    public int? DailyLogId { get; set; }
    public DailyLog? DailyLog { get; set; }
}
