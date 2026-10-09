using System.Linq.Expressions;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Domain.Common;
using AquaTrack.Domain.Entities;
using AquaTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AquaTrack.Infrastructure.Persistence;

/// <summary>
/// The EF Core implementation of <see cref="IApplicationDbContext"/>. Application services only
/// ever see the interface; the provider, migrations and Identity tables stay down here.
/// </summary>
public class AquaTrackDbContext(
    DbContextOptions<AquaTrackDbContext> options,
    ICurrentUser? currentUser = null)
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options), IApplicationDbContext
{
    // Farm
    public DbSet<Pond> Ponds => Set<Pond>();
    public DbSet<FishType> FishTypes => Set<FishType>();
    public DbSet<FishStock> FishStocks => Set<FishStock>();
    public DbSet<FishGrowth> FishGrowths => Set<FishGrowth>();
    public DbSet<FishMortality> FishMortalities => Set<FishMortality>();
    public DbSet<Harvest> Harvests => Set<Harvest>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<WaterQualityLog> WaterQualityLogs => Set<WaterQualityLog>();

    // Inventory
    public DbSet<FeedItem> FeedItems => Set<FeedItem>();
    public DbSet<FeedPurchase> FeedPurchases => Set<FeedPurchase>();
    public DbSet<FeedUsage> FeedUsages => Set<FeedUsage>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<MedicinePurchase> MedicinePurchases => Set<MedicinePurchase>();
    public DbSet<MedicineUsage> MedicineUsages => Set<MedicineUsage>();
    public DbSet<Equipment> Equipments => Set<Equipment>();
    public DbSet<EquipmentPart> EquipmentParts => Set<EquipmentPart>();
    public DbSet<EquipmentMaintenance> EquipmentMaintenances => Set<EquipmentMaintenance>();

    // Parties and commerce
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

    // Finance
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<OtherIncome> OtherIncomes => Set<OtherIncome>();
    public DbSet<CashTransaction> CashTransactions => Set<CashTransaction>();

    // Operations and AI
    public DbSet<DailyLog> DailyLogs => Set<DailyLog>();
    public DbSet<AiRecommendation> AiRecommendations => Set<AiRecommendation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<FarmSetting> FarmSettings => Set<FarmSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AquaTrackDbContext).Assembly);

        ApplySharedConventions(builder);
        ApplySoftDeleteFilter(builder);
    }

    /// <summary>
    /// Keeps column shapes consistent without repeating attributes on 30-odd entities:
    /// money at (18,2), quantities at (18,3), and no cascading deletes by default.
    /// </summary>
    private static void ApplySharedConventions(ModelBuilder builder)
    {
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType != typeof(decimal) && property.ClrType != typeof(decimal?))
                    continue;

                var isQuantity = property.Name.Contains("Quantity", StringComparison.Ordinal)
                                 || property.Name.EndsWith("Kg", StringComparison.Ordinal)
                                 || property.Name.EndsWith("Gram", StringComparison.Ordinal)
                                 || property.Name.Contains("Percent", StringComparison.Ordinal)
                                 || property.Name.Contains("Stock", StringComparison.Ordinal)
                                 || property.Name.Contains("Level", StringComparison.Ordinal);

                property.SetPrecision(isQuantity ? Precision.QuantityPrecision : Precision.MoneyPrecision);
                property.SetScale(isQuantity ? Precision.QuantityScale : Precision.MoneyScale);
            }

            foreach (var fk in entity.GetForeignKeys())
                fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    /// <summary>Rows are never physically removed; every query filters <c>IsDeleted == false</c>.</summary>
    private static void ApplySoftDeleteFilter(ModelBuilder builder)
    {
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entity.ClrType)) continue;

            var parameter = Expression.Parameter(entity.ClrType, "e");
            var body = Expression.Not(Expression.Property(parameter, nameof(BaseEntity.IsDeleted)));
            builder.Entity(entity.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    public override int SaveChanges()
    {
        StampAudit();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    public async Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => new EfDatabaseTransaction(await Database.BeginTransactionAsync(ct));

    /// <summary>
    /// Fills audit columns and converts hard deletes into soft deletes, so history and reports
    /// survive a delete and every row records who last touched it.
    /// </summary>
    private void StampAudit()
    {
        var now = DateTime.UtcNow;
        var user = currentUser?.UserName;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedBy ??= user;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.UpdatedBy = user;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAtUtc = now;
                    entry.Entity.UpdatedBy = user;
                    break;
            }
        }
    }
}

/// <summary>Adapts an EF Core transaction to the application layer's abstraction.</summary>
internal sealed class EfDatabaseTransaction(IDbContextTransaction transaction) : IDatabaseTransaction
{
    public Task CommitAsync(CancellationToken ct = default) => transaction.CommitAsync(ct);

    public Task RollbackAsync(CancellationToken ct = default) => transaction.RollbackAsync(ct);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
