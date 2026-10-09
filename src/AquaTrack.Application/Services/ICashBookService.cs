using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Application.Services;

public record CashSummary(
    decimal OpeningBalance,
    decimal TotalIn,
    decimal TotalOut,
    decimal ClosingBalance)
{
    public decimal Net => TotalIn - TotalOut;
}

public record CashLedgerRow(CashTransaction Transaction, decimal RunningBalance);

/// <summary>
/// The single entry point for writing to the cash book. Every module posts through this so the
/// ledger stays the one source of truth for "Opening + In - Out = Closing".
/// </summary>
public interface ICashBookService
{
    Task<CashTransaction> PostAsync(
        DateOnly date,
        CashDirection direction,
        decimal amount,
        string category,
        PaymentMethod method = PaymentMethod.Cash,
        string? description = null,
        ReferenceType referenceType = ReferenceType.Manual,
        int? referenceId = null,
        int? customerId = null,
        int? supplierId = null,
        bool systemGenerated = true,
        CancellationToken ct = default);

    /// <summary>Removes any ledger rows a document previously created, so it can be re-posted cleanly.</summary>
    Task ReverseAsync(ReferenceType referenceType, int referenceId, CancellationToken ct = default);

    /// <summary>Net cash movement strictly before <paramref name="date"/>, plus the configured opening balance.</summary>
    Task<decimal> GetOpeningBalanceAsync(DateOnly date, CancellationToken ct = default);

    Task<CashSummary> GetSummaryAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<IReadOnlyList<CashLedgerRow>> GetLedgerAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}
