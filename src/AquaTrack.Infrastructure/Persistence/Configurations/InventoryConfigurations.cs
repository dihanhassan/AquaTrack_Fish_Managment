using AquaTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaTrack.Infrastructure.Persistence.Configurations;

public class FeedItemConfiguration : IEntityTypeConfiguration<FeedItem>
{
    public void Configure(EntityTypeBuilder<FeedItem> b)
    {
        b.Property(f => f.Name).IsRequired().HasMaxLength(150);
        b.Property(f => f.Brand).HasMaxLength(120);
        b.Ignore(f => f.StockValue);
        b.Ignore(f => f.IsLowStock);
        b.HasOne(f => f.Supplier).WithMany().HasForeignKey(f => f.SupplierId);
        b.HasIndex(f => f.Name);
    }
}

public class FeedPurchaseConfiguration : IEntityTypeConfiguration<FeedPurchase>
{
    public void Configure(EntityTypeBuilder<FeedPurchase> b)
    {
        b.Property(p => p.InvoiceNo).HasMaxLength(60);
        b.Property(p => p.Notes).HasMaxLength(500);
        b.Ignore(p => p.DueAmount);
        b.HasOne(p => p.FeedItem).WithMany(f => f.Purchases).HasForeignKey(p => p.FeedItemId);
        b.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId);
        b.HasIndex(p => p.PurchaseDate);
    }
}

public class FeedUsageConfiguration : IEntityTypeConfiguration<FeedUsage>
{
    public void Configure(EntityTypeBuilder<FeedUsage> b)
    {
        b.Property(u => u.Notes).HasMaxLength(500);
        b.HasOne(u => u.FeedItem).WithMany(f => f.Usages).HasForeignKey(u => u.FeedItemId);
        b.HasOne(u => u.Pond).WithMany(p => p.FeedUsages).HasForeignKey(u => u.PondId);
        b.HasOne(u => u.DailyLog).WithMany().HasForeignKey(u => u.DailyLogId);
        b.HasIndex(u => new { u.PondId, u.UsageDate });
        b.HasIndex(u => u.UsageDate);
    }
}

public class MedicineConfiguration : IEntityTypeConfiguration<Medicine>
{
    public void Configure(EntityTypeBuilder<Medicine> b)
    {
        b.Property(m => m.Name).IsRequired().HasMaxLength(150);
        b.Property(m => m.Brand).HasMaxLength(120);
        b.Property(m => m.Dosage).HasMaxLength(250);
        b.Ignore(m => m.StockValue);
        b.Ignore(m => m.IsLowStock);
        b.Ignore(m => m.IsExpired);
        b.Ignore(m => m.IsExpiringSoon);
        b.HasOne(m => m.Supplier).WithMany().HasForeignKey(m => m.SupplierId);
        b.HasIndex(m => m.Name);
        b.HasIndex(m => m.ExpiryDate);
    }
}

public class MedicinePurchaseConfiguration : IEntityTypeConfiguration<MedicinePurchase>
{
    public void Configure(EntityTypeBuilder<MedicinePurchase> b)
    {
        b.Property(p => p.BatchNo).HasMaxLength(60);
        b.Property(p => p.Notes).HasMaxLength(500);
        b.Ignore(p => p.DueAmount);
        b.HasOne(p => p.Medicine).WithMany(m => m.Purchases).HasForeignKey(p => p.MedicineId);
        b.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId);
        b.HasIndex(p => p.PurchaseDate);
    }
}

public class MedicineUsageConfiguration : IEntityTypeConfiguration<MedicineUsage>
{
    public void Configure(EntityTypeBuilder<MedicineUsage> b)
    {
        b.Property(u => u.Purpose).HasMaxLength(250);
        b.Property(u => u.Notes).HasMaxLength(500);
        b.HasOne(u => u.Medicine).WithMany(m => m.Usages).HasForeignKey(u => u.MedicineId);
        b.HasOne(u => u.Pond).WithMany(p => p.MedicineUsages).HasForeignKey(u => u.PondId);
        b.HasOne(u => u.DailyLog).WithMany().HasForeignKey(u => u.DailyLogId);
        b.HasIndex(u => new { u.PondId, u.UsageDate });
    }
}

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> b)
    {
        b.Property(e => e.Name).IsRequired().HasMaxLength(150);
        b.Property(e => e.Code).HasMaxLength(40);
        b.Property(e => e.Brand).HasMaxLength(120);
        b.Property(e => e.ModelNo).HasMaxLength(80);
        b.Property(e => e.SerialNo).HasMaxLength(80);
        b.Property(e => e.Notes).HasMaxLength(1000);
        b.Ignore(e => e.TotalPurchaseCost);
        b.Ignore(e => e.IsUnderWarranty);
        b.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId);
        b.HasOne(e => e.Pond).WithMany().HasForeignKey(e => e.PondId);
        b.HasIndex(e => e.Name);
        b.HasIndex(e => e.Category);
    }
}

public class EquipmentPartConfiguration : IEntityTypeConfiguration<EquipmentPart>
{
    public void Configure(EntityTypeBuilder<EquipmentPart> b)
    {
        b.Property(p => p.Name).IsRequired().HasMaxLength(150);
        b.Property(p => p.PartNo).HasMaxLength(60);
        b.Property(p => p.Notes).HasMaxLength(500);
        b.Ignore(p => p.StockValue);
        b.Ignore(p => p.IsLowStock);
        b.HasOne(p => p.Equipment).WithMany(e => e.Parts).HasForeignKey(p => p.EquipmentId);
    }
}

public class EquipmentMaintenanceConfiguration : IEntityTypeConfiguration<EquipmentMaintenance>
{
    public void Configure(EntityTypeBuilder<EquipmentMaintenance> b)
    {
        b.Property(m => m.Description).HasMaxLength(500);
        b.Property(m => m.PerformedBy).HasMaxLength(150);
        b.Property(m => m.Notes).HasMaxLength(500);
        b.HasOne(m => m.Equipment).WithMany(e => e.MaintenanceRecords).HasForeignKey(m => m.EquipmentId);
        b.HasIndex(m => m.MaintenanceDate);
        b.HasIndex(m => m.NextDueDate);
    }
}
