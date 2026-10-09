using AquaTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaTrack.Infrastructure.Persistence.Configurations;

public class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> b)
    {
        b.Property(c => c.Name).IsRequired().HasMaxLength(120);
        b.Property(c => c.Description).HasMaxLength(500);
        b.HasIndex(c => c.Name).IsUnique();
    }
}

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> b)
    {
        b.Property(e => e.Description).HasMaxLength(500);
        b.Property(e => e.VoucherNo).HasMaxLength(40);
        b.HasOne(e => e.ExpenseCategory).WithMany(c => c.Expenses).HasForeignKey(e => e.ExpenseCategoryId);
        b.HasOne(e => e.Pond).WithMany().HasForeignKey(e => e.PondId);
        b.HasOne(e => e.DailyLog).WithMany().HasForeignKey(e => e.DailyLogId);
        b.HasIndex(e => e.ExpenseDate);
        b.HasIndex(e => new { e.ExpenseCategoryId, e.ExpenseDate });
        b.HasIndex(e => new { e.ReferenceType, e.ReferenceId });
    }
}

public class OtherIncomeConfiguration : IEntityTypeConfiguration<OtherIncome>
{
    public void Configure(EntityTypeBuilder<OtherIncome> b)
    {
        b.Property(i => i.Source).IsRequired().HasMaxLength(200);
        b.Property(i => i.Description).HasMaxLength(500);
        b.HasOne(i => i.DailyLog).WithMany().HasForeignKey(i => i.DailyLogId);
        b.HasIndex(i => i.IncomeDate);
    }
}

public class CashTransactionConfiguration : IEntityTypeConfiguration<CashTransaction>
{
    public void Configure(EntityTypeBuilder<CashTransaction> b)
    {
        b.Property(t => t.Category).IsRequired().HasMaxLength(120);
        b.Property(t => t.Description).HasMaxLength(500);
        b.Property(t => t.VoucherNo).HasMaxLength(40);
        b.Ignore(t => t.SignedAmount);
        b.HasOne(t => t.Customer).WithMany().HasForeignKey(t => t.CustomerId);
        b.HasOne(t => t.Supplier).WithMany().HasForeignKey(t => t.SupplierId);
        b.HasIndex(t => t.TransactionDate);
        b.HasIndex(t => new { t.TransactionDate, t.Direction });
        b.HasIndex(t => new { t.ReferenceType, t.ReferenceId });
    }
}

public class DailyLogConfiguration : IEntityTypeConfiguration<DailyLog>
{
    public void Configure(EntityTypeBuilder<DailyLog> b)
    {
        b.Property(d => d.Weather).HasMaxLength(120);
        b.Property(d => d.Observations).HasMaxLength(2000);
        b.Ignore(d => d.NetResult);
        b.HasOne(d => d.Pond).WithMany().HasForeignKey(d => d.PondId);
        b.HasIndex(d => new { d.PondId, d.LogDate }).IsUnique();
        b.HasIndex(d => d.LogDate);
    }
}

public class AiRecommendationConfiguration : IEntityTypeConfiguration<AiRecommendation>
{
    public void Configure(EntityTypeBuilder<AiRecommendation> b)
    {
        b.Property(r => r.Title).IsRequired().HasMaxLength(250);
        b.Property(r => r.Body).IsRequired().HasMaxLength(4000);
        b.Property(r => r.Rationale).HasMaxLength(2000);
        b.Property(r => r.Unit).HasMaxLength(40);
        b.HasOne(r => r.Pond).WithMany().HasForeignKey(r => r.PondId);
        b.HasOne(r => r.FishStock).WithMany().HasForeignKey(r => r.FishStockId);
        b.HasIndex(r => new { r.Type, r.GeneratedOn });
        b.HasIndex(r => r.Status);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(n => n.Title).IsRequired().HasMaxLength(250);
        b.Property(n => n.Message).IsRequired().HasMaxLength(1000);
        b.Property(n => n.Link).HasMaxLength(300);
        b.Property(n => n.DedupeKey).HasMaxLength(200);
        b.HasIndex(n => n.IsRead);
        b.HasIndex(n => n.DedupeKey);
    }
}

public class FarmSettingConfiguration : IEntityTypeConfiguration<FarmSetting>
{
    public void Configure(EntityTypeBuilder<FarmSetting> b)
    {
        b.Property(s => s.Key).IsRequired().HasMaxLength(100);
        b.Property(s => s.Value).HasMaxLength(1000);
        b.Property(s => s.DisplayName).HasMaxLength(150);
        b.Property(s => s.Description).HasMaxLength(500);
        b.Property(s => s.Group).HasMaxLength(60);
        b.HasIndex(s => s.Key).IsUnique();
    }
}
