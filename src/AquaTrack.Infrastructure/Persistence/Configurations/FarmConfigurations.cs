using AquaTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquaTrack.Infrastructure.Persistence.Configurations;

public class PondConfiguration : IEntityTypeConfiguration<Pond>
{
    public void Configure(EntityTypeBuilder<Pond> b)
    {
        b.Property(p => p.Name).IsRequired().HasMaxLength(120);
        b.Property(p => p.Code).HasMaxLength(40);
        b.Property(p => p.Location).HasMaxLength(250);
        b.Property(p => p.Notes).HasMaxLength(1000);
        b.Ignore(p => p.VolumeCubicMetre);
        b.HasIndex(p => p.Name);
        b.HasIndex(p => p.Code).IsUnique().HasFilter(null);
    }
}

public class FishTypeConfiguration : IEntityTypeConfiguration<FishType>
{
    public void Configure(EntityTypeBuilder<FishType> b)
    {
        b.Property(f => f.Name).IsRequired().HasMaxLength(120);
        b.Property(f => f.LocalName).HasMaxLength(120);
        b.Property(f => f.ScientificName).HasMaxLength(150);
        b.Property(f => f.Description).HasMaxLength(1000);
        b.HasIndex(f => f.Name).IsUnique();
    }
}

public class FishStockConfiguration : IEntityTypeConfiguration<FishStock>
{
    public void Configure(EntityTypeBuilder<FishStock> b)
    {
        b.Property(s => s.BatchCode).HasMaxLength(60);
        b.Property(s => s.Source).HasMaxLength(200);
        b.Property(s => s.Notes).HasMaxLength(1000);
        b.Ignore(s => s.TotalStockingCost);
        b.Ignore(s => s.BiomassKg);
        b.Ignore(s => s.TotalMortality);

        b.HasOne(s => s.Pond).WithMany(p => p.FishStocks).HasForeignKey(s => s.PondId);
        b.HasOne(s => s.FishType).WithMany(f => f.FishStocks).HasForeignKey(s => s.FishTypeId);

        // Dashboard and pond pages always filter on these two together.
        b.HasIndex(s => new { s.PondId, s.Status });
        b.HasIndex(s => s.StockDate);
    }
}

public class FishGrowthConfiguration : IEntityTypeConfiguration<FishGrowth>
{
    public void Configure(EntityTypeBuilder<FishGrowth> b)
    {
        b.Property(g => g.Notes).HasMaxLength(500);
        b.HasOne(g => g.FishStock).WithMany(s => s.GrowthRecords).HasForeignKey(g => g.FishStockId);
        b.HasIndex(g => new { g.FishStockId, g.RecordDate });
    }
}

public class FishMortalityConfiguration : IEntityTypeConfiguration<FishMortality>
{
    public void Configure(EntityTypeBuilder<FishMortality> b)
    {
        b.Property(m => m.Notes).HasMaxLength(500);
        b.HasOne(m => m.FishStock).WithMany(s => s.MortalityRecords).HasForeignKey(m => m.FishStockId);
        b.HasIndex(m => new { m.FishStockId, m.RecordDate });
        b.HasIndex(m => m.RecordDate);
    }
}

public class HarvestConfiguration : IEntityTypeConfiguration<Harvest>
{
    public void Configure(EntityTypeBuilder<Harvest> b)
    {
        b.Property(h => h.Notes).HasMaxLength(500);
        b.Ignore(h => h.AvgWeightGram);
        b.HasOne(h => h.FishStock).WithMany(s => s.Harvests).HasForeignKey(h => h.FishStockId);
        b.HasIndex(h => h.HarvestDate);
    }
}

public class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> b)
    {
        b.Property(a => a.Notes).HasMaxLength(500);
        b.HasOne(a => a.FishStock).WithMany(s => s.Adjustments).HasForeignKey(a => a.FishStockId);
        b.HasIndex(a => a.AdjustmentDate);
    }
}

public class WaterQualityLogConfiguration : IEntityTypeConfiguration<WaterQualityLog>
{
    public void Configure(EntityTypeBuilder<WaterQualityLog> b)
    {
        b.Property(w => w.Notes).HasMaxLength(500);
        b.HasOne(w => w.Pond).WithMany(p => p.WaterQualityLogs).HasForeignKey(w => w.PondId);
        b.HasIndex(w => new { w.PondId, w.RecordDate });
    }
}
