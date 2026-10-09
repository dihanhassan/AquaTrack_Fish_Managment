using AquaTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaTrack.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.Property(s => s.Name).IsRequired().HasMaxLength(150);
        b.Property(s => s.ContactPerson).HasMaxLength(150);
        b.Property(s => s.Phone).HasMaxLength(40);
        b.Property(s => s.Email).HasMaxLength(150);
        b.Property(s => s.Address).HasMaxLength(400);
        b.Property(s => s.Notes).HasMaxLength(1000);
        b.HasIndex(s => s.Name);
        b.HasIndex(s => s.Phone);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.Property(c => c.Name).IsRequired().HasMaxLength(150);
        b.Property(c => c.Phone).HasMaxLength(40);
        b.Property(c => c.Email).HasMaxLength(150);
        b.Property(c => c.Address).HasMaxLength(400);
        b.Property(c => c.Notes).HasMaxLength(1000);
        b.HasIndex(c => c.Name);
        b.HasIndex(c => c.Phone);
    }
}

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> b)
    {
        b.Property(p => p.PurchaseNo).IsRequired().HasMaxLength(40);
        b.Property(p => p.InvoiceNo).HasMaxLength(60);
        b.Property(p => p.Notes).HasMaxLength(1000);
        b.Ignore(p => p.DueAmount);
        b.Ignore(p => p.IsFullyPaid);
        b.HasOne(p => p.Supplier).WithMany(s => s.Purchases).HasForeignKey(p => p.SupplierId);
        b.HasMany(p => p.Items).WithOne(i => i.Purchase).HasForeignKey(i => i.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(p => p.PurchaseNo).IsUnique();
        b.HasIndex(p => p.PurchaseDate);
    }
}

public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> b)
    {
        b.Property(i => i.Description).IsRequired().HasMaxLength(250);
        b.HasOne(i => i.FeedItem).WithMany().HasForeignKey(i => i.FeedItemId);
        b.HasOne(i => i.Medicine).WithMany().HasForeignKey(i => i.MedicineId);
        b.HasOne(i => i.Equipment).WithMany().HasForeignKey(i => i.EquipmentId);
        b.HasOne(i => i.FishType).WithMany().HasForeignKey(i => i.FishTypeId);
        b.HasIndex(i => i.PurchaseId);
    }
}

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.Property(s => s.InvoiceNo).IsRequired().HasMaxLength(40);
        b.Property(s => s.Notes).HasMaxLength(1000);
        b.Ignore(s => s.DueAmount);
        b.Ignore(s => s.IsFullyPaid);
        b.HasOne(s => s.Customer).WithMany(c => c.Sales).HasForeignKey(s => s.CustomerId);
        b.HasOne(s => s.DailyLog).WithMany().HasForeignKey(s => s.DailyLogId);
        b.HasMany(s => s.Items).WithOne(i => i.Sale).HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(s => s.InvoiceNo).IsUnique();
        b.HasIndex(s => s.SaleDate);
    }
}

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> b)
    {
        b.HasOne(i => i.FishType).WithMany().HasForeignKey(i => i.FishTypeId);
        b.HasOne(i => i.Pond).WithMany().HasForeignKey(i => i.PondId);
        b.HasOne(i => i.FishStock).WithMany().HasForeignKey(i => i.FishStockId);
        b.HasIndex(i => i.SaleId);
    }
}
