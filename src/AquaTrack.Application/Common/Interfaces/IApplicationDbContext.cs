using AquaTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Application.Common.Interfaces;

/// <summary>
/// The persistence surface the application layer is allowed to see. Infrastructure supplies the
/// EF Core implementation, so use cases depend on this abstraction rather than on a concrete
/// DbContext, a provider, or a connection.
/// </summary>
public interface IApplicationDbContext
{
    // Farm
    DbSet<Pond> Ponds { get; }
    DbSet<FishType> FishTypes { get; }
    DbSet<FishStock> FishStocks { get; }
    DbSet<FishGrowth> FishGrowths { get; }
    DbSet<FishMortality> FishMortalities { get; }
    DbSet<Harvest> Harvests { get; }
    DbSet<StockAdjustment> StockAdjustments { get; }
    DbSet<WaterQualityLog> WaterQualityLogs { get; }

    // Inventory
    DbSet<FeedItem> FeedItems { get; }
    DbSet<FeedPurchase> FeedPurchases { get; }
    DbSet<FeedUsage> FeedUsages { get; }
    DbSet<Medicine> Medicines { get; }
    DbSet<MedicinePurchase> MedicinePurchases { get; }
    DbSet<MedicineUsage> MedicineUsages { get; }
    DbSet<Equipment> Equipments { get; }
    DbSet<EquipmentPart> EquipmentParts { get; }
    DbSet<EquipmentMaintenance> EquipmentMaintenances { get; }

    // Parties and commerce
    DbSet<Supplier> Suppliers { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Purchase> Purchases { get; }
    DbSet<PurchaseItem> PurchaseItems { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }

    // Finance
    DbSet<ExpenseCategory> ExpenseCategories { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<OtherIncome> OtherIncomes { get; }
    DbSet<CashTransaction> CashTransactions { get; }

    // Operations and AI
    DbSet<DailyLog> DailyLogs { get; }
    DbSet<AiRecommendation> AiRecommendations { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<FarmSetting> FarmSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Opens a transaction so a use case that writes several aggregates commits all or nothing.
    /// Returned as an abstraction to keep EF Core transaction types out of the application layer.
    /// </summary>
    Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

/// <summary>A unit of work the application layer can commit or abandon.</summary>
public interface IDatabaseTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
