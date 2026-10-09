using AquaTrack.Application.Common.Models;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaTrack.Infrastructure.Persistence.Seed;

/// <summary>
/// Generates six months of plausible farm activity so the dashboard, reports and AI module have
/// something to work with on a fresh install. Enabled with <c>AquaTrack:SeedDemoData</c>.
/// </summary>
public static class DemoDataSeeder
{
    // Fixed seed keeps demo runs reproducible, which makes screenshots and bug reports comparable.
    private static readonly Random Rng = new(20261009);

    public static async Task SeedAsync(AquaTrackDbContext db, ILogger logger, CancellationToken ct = default)
    {
        if (await db.Ponds.AnyAsync(ct))
        {
            logger.LogInformation("Demo data skipped: ponds already exist.");
            return;
        }

        logger.LogInformation("Seeding demo data...");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = today.AddMonths(-6);

        var suppliers = await SeedSuppliersAsync(db, ct);
        var customers = await SeedCustomersAsync(db, ct);
        var ponds = await SeedPondsAsync(db, ct);
        var feedItems = await SeedFeedItemsAsync(db, suppliers, ct);
        var medicines = await SeedMedicinesAsync(db, suppliers, ct);
        await SeedEquipmentAsync(db, suppliers, ponds, ct);

        var stocks = await SeedFishStocksAsync(db, ponds, start, ct);
        await SeedOpeningBalanceAsync(db, start, ct);
        await SeedDailyActivityAsync(db, ponds, stocks, feedItems, medicines, customers, start, today, ct);

        logger.LogInformation("Demo data seeded: {Ponds} ponds, {Stocks} batches.", ponds.Count, stocks.Count);
    }

    /// <summary>
    /// A farm starts with working capital. Without it the cash book would open at zero and the
    /// first months of feed and wages would read as a large negative balance.
    /// </summary>
    private static async Task SeedOpeningBalanceAsync(AquaTrackDbContext db, DateOnly start, CancellationToken ct)
    {
        const decimal openingCapital = 1_500_000m;

        // Posted as a ledger row rather than written to Finance.OpeningCashBalance: the balance
        // calculation adds the setting to every prior movement, so doing both would double-count.
        db.CashTransactions.Add(new CashTransaction
        {
            TransactionDate = start.AddDays(-1),
            Direction = CashDirection.In,
            Amount = openingCapital,
            Category = "Opening balance",
            PaymentMethod = PaymentMethod.Cash,
            Description = "Working capital at the start of the season",
            ReferenceType = ReferenceType.OpeningBalance,
            IsSystemGenerated = true,
        });

        await db.SaveChangesAsync(ct);
    }

    private static async Task<List<Supplier>> SeedSuppliersAsync(AquaTrackDbContext db, CancellationToken ct)
    {
        var suppliers = new List<Supplier>
        {
            new() { Name = "Mega Feed Mills Ltd.", ContactPerson = "Rafiqul Islam", Phone = "01711000111", Address = "Bogura" },
            new() { Name = "Aqua Care Pharma", ContactPerson = "Nazmul Haque", Phone = "01811000222", Address = "Dhaka" },
            new() { Name = "Sonali Hatchery", ContactPerson = "Abdul Karim", Phone = "01911000333", Address = "Mymensingh" },
            new() { Name = "Bengal Machinery", ContactPerson = "Shahin Alam", Phone = "01611000444", Address = "Jessore" },
        };

        db.Suppliers.AddRange(suppliers);
        await db.SaveChangesAsync(ct);
        return suppliers;
    }

    private static async Task<List<Customer>> SeedCustomersAsync(AquaTrackDbContext db, CancellationToken ct)
    {
        var customers = new List<Customer>
        {
            new() { Name = "Karwan Bazar Arat", Phone = "01712345678", Address = "Dhaka" },
            new() { Name = "Hasan Fish Traders", Phone = "01812345678", Address = "Narayanganj" },
            new() { Name = "Local Bazar Retail", Phone = "01912345678", Address = "Local market" },
            new() { Name = "Rupali Fish House", Phone = "01612345678", Address = "Savar" },
        };

        db.Customers.AddRange(customers);
        await db.SaveChangesAsync(ct);
        return customers;
    }

    private static async Task<List<Pond>> SeedPondsAsync(AquaTrackDbContext db, CancellationToken ct)
    {
        var ponds = new List<Pond>
        {
            new() { Name = "Pond-01", Code = "P01", AreaDecimal = 60, DepthMetre = 2.0m, WaterType = WaterType.Freshwater, Status = PondStatus.Active, Location = "North block" },
            new() { Name = "Pond-02", Code = "P02", AreaDecimal = 45, DepthMetre = 1.8m, WaterType = WaterType.Freshwater, Status = PondStatus.Active, Location = "North block" },
            new() { Name = "Pond-03", Code = "P03", AreaDecimal = 80, DepthMetre = 2.5m, WaterType = WaterType.Freshwater, Status = PondStatus.Active, Location = "South block" },
            new() { Name = "Pond-04", Code = "P04", AreaDecimal = 30, DepthMetre = 1.5m, WaterType = WaterType.Freshwater, Status = PondStatus.UnderMaintenance, Location = "South block" },
        };

        db.Ponds.AddRange(ponds);
        await db.SaveChangesAsync(ct);
        return ponds;
    }

    private static async Task<List<FeedItem>> SeedFeedItemsAsync(
        AquaTrackDbContext db, List<Supplier> suppliers, CancellationToken ct)
    {
        var feedSupplier = suppliers[0];

        var items = new List<FeedItem>
        {
            new() { Name = "Floating Feed (Grower)", Brand = "Mega Fish", FeedType = FeedType.Floating, Unit = UnitOfMeasure.Kg, SupplierId = feedSupplier.Id, CurrentStock = 850, ReorderLevel = 200, AverageUnitCost = 62m, LastUnitPrice = 62m, ProteinPercent = 28 },
            new() { Name = "Sinking Feed (Carp)", Brand = "Aqua Plus", FeedType = FeedType.Sinking, Unit = UnitOfMeasure.Kg, SupplierId = feedSupplier.Id, CurrentStock = 420, ReorderLevel = 150, AverageUnitCost = 54m, LastUnitPrice = 54m, ProteinPercent = 24 },
            new() { Name = "Starter Crumble", Brand = "Mega Fish", FeedType = FeedType.Starter, Unit = UnitOfMeasure.Kg, SupplierId = feedSupplier.Id, CurrentStock = 90, ReorderLevel = 100, AverageUnitCost = 78m, LastUnitPrice = 78m, ProteinPercent = 35 },
            new() { Name = "Rice Bran", Brand = "Local", FeedType = FeedType.Supplement, Unit = UnitOfMeasure.Kg, SupplierId = feedSupplier.Id, CurrentStock = 600, ReorderLevel = 150, AverageUnitCost = 26m, LastUnitPrice = 26m, ProteinPercent = 12 },
        };

        db.FeedItems.AddRange(items);
        await db.SaveChangesAsync(ct);
        return items;
    }

    private static async Task<List<Medicine>> SeedMedicinesAsync(
        AquaTrackDbContext db, List<Supplier> suppliers, CancellationToken ct)
    {
        var pharma = suppliers[1];
        var today = DateOnly.FromDateTime(DateTime.Today);

        var medicines = new List<Medicine>
        {
            new() { Name = "Oxytetracycline", Brand = "AquaMed", MedicineType = MedicineType.Antibiotic, Unit = UnitOfMeasure.Kg, SupplierId = pharma.Id, CurrentStock = 12, ReorderLevel = 5, AverageUnitCost = 1250m, LastUnitPrice = 1250m, ExpiryDate = today.AddMonths(8), Dosage = "5 g per kg feed for 5 days" },
            new() { Name = "Pond Lime (CaCO3)", Brand = "Local", MedicineType = MedicineType.Lime, Unit = UnitOfMeasure.Kg, SupplierId = pharma.Id, CurrentStock = 320, ReorderLevel = 100, AverageUnitCost = 18m, LastUnitPrice = 18m, Dosage = "1 kg per decimal" },
            new() { Name = "Probiotic Plus", Brand = "BioAqua", MedicineType = MedicineType.Probiotic, Unit = UnitOfMeasure.Litre, SupplierId = pharma.Id, CurrentStock = 18, ReorderLevel = 10, AverageUnitCost = 850m, LastUnitPrice = 850m, ExpiryDate = today.AddMonths(3) },
            new() { Name = "Potassium Permanganate", Brand = "AquaMed", MedicineType = MedicineType.Disinfectant, Unit = UnitOfMeasure.Kg, SupplierId = pharma.Id, CurrentStock = 4, ReorderLevel = 5, AverageUnitCost = 520m, LastUnitPrice = 520m, ExpiryDate = today.AddDays(20) },
            new() { Name = "Vitamin C Premix", Brand = "NutriFish", MedicineType = MedicineType.Vitamin, Unit = UnitOfMeasure.Kg, SupplierId = pharma.Id, CurrentStock = 9, ReorderLevel = 4, AverageUnitCost = 680m, LastUnitPrice = 680m, ExpiryDate = today.AddMonths(14) },
        };

        db.Medicines.AddRange(medicines);
        await db.SaveChangesAsync(ct);
        return medicines;
    }

    private static async Task SeedEquipmentAsync(
        AquaTrackDbContext db, List<Supplier> suppliers, List<Pond> ponds, CancellationToken ct)
    {
        var machinery = suppliers[3];
        var today = DateOnly.FromDateTime(DateTime.Today);

        var equipment = new List<Equipment>
        {
            new() { Name = "Water Pump 5HP", Code = "EQ-01", Category = EquipmentCategory.WaterPump, Brand = "Honda", SupplierId = machinery.Id, PurchaseDate = today.AddMonths(-14), PurchasePrice = 48000m, Quantity = 1, Condition = EquipmentCondition.Good, WarrantyUntil = today.AddMonths(-2), PondId = ponds[0].Id },
            new() { Name = "Paddle Wheel Aerator", Code = "EQ-02", Category = EquipmentCategory.Aerator, Brand = "AquaTech", SupplierId = machinery.Id, PurchaseDate = today.AddMonths(-9), PurchasePrice = 62000m, Quantity = 2, Condition = EquipmentCondition.Good, WarrantyUntil = today.AddMonths(15), PondId = ponds[2].Id },
            new() { Name = "Drag Net 100ft", Code = "EQ-03", Category = EquipmentCategory.FishingNet, SupplierId = machinery.Id, PurchaseDate = today.AddMonths(-5), PurchasePrice = 18500m, Quantity = 1, Condition = EquipmentCondition.Good },
            new() { Name = "Water Testing Kit", Code = "EQ-04", Category = EquipmentCategory.WaterTestingKit, Brand = "Hanna", SupplierId = machinery.Id, PurchaseDate = today.AddMonths(-3), PurchasePrice = 12500m, Quantity = 1, Condition = EquipmentCondition.New },
            new() { Name = "Diesel Generator 10KVA", Code = "EQ-05", Category = EquipmentCategory.Generator, SupplierId = machinery.Id, PurchaseDate = today.AddMonths(-20), PurchasePrice = 145000m, Quantity = 1, Condition = EquipmentCondition.NeedsRepair },
        };

        db.Equipments.AddRange(equipment);
        await db.SaveChangesAsync(ct);

        db.EquipmentParts.AddRange(
            new EquipmentPart { EquipmentId = equipment[0].Id, Name = "Impeller", Unit = UnitOfMeasure.Piece, Quantity = 2, ReorderLevel = 1, UnitPrice = 3200m, PurchaseDate = today.AddMonths(-6) },
            new EquipmentPart { EquipmentId = equipment[0].Id, Name = "Mechanical Seal", Unit = UnitOfMeasure.Piece, Quantity = 1, ReorderLevel = 2, UnitPrice = 1450m, PurchaseDate = today.AddMonths(-4) },
            new EquipmentPart { EquipmentId = equipment[1].Id, Name = "Paddle Blade", Unit = UnitOfMeasure.Piece, Quantity = 6, ReorderLevel = 4, UnitPrice = 850m, PurchaseDate = today.AddMonths(-2) });

        db.EquipmentMaintenances.AddRange(
            new EquipmentMaintenance { EquipmentId = equipment[0].Id, MaintenanceDate = today.AddMonths(-3), MaintenanceType = MaintenanceType.Routine, Description = "Oil change and impeller cleaning", Cost = 2500m, PerformedBy = "Local mechanic", NextDueDate = today.AddDays(5), ConditionAfter = EquipmentCondition.Good },
            new EquipmentMaintenance { EquipmentId = equipment[4].Id, MaintenanceDate = today.AddMonths(-1), MaintenanceType = MaintenanceType.Repair, Description = "Alternator rewinding", Cost = 14500m, PerformedBy = "Bengal Machinery", NextDueDate = today.AddMonths(5), ConditionAfter = EquipmentCondition.NeedsRepair });

        await db.SaveChangesAsync(ct);
    }

    private static async Task<List<FishStock>> SeedFishStocksAsync(
        AquaTrackDbContext db, List<Pond> ponds, DateOnly start, CancellationToken ct)
    {
        var fishTypes = await db.FishTypes.OrderBy(f => f.Id).ToListAsync(ct);

        var stocks = new List<FishStock>
        {
            new() { PondId = ponds[0].Id, FishTypeId = fishTypes[0].Id, BatchCode = "B-P01-RUI", StockDate = start, InitialQuantity = 2000, CurrentQuantity = 2000, InitialAvgWeightGram = 25m, CurrentAvgWeightGram = 25m, UnitCost = 8m, Source = "Sonali Hatchery" },
            new() { PondId = ponds[0].Id, FishTypeId = fishTypes[1].Id, BatchCode = "B-P01-KAT", StockDate = start.AddDays(5), InitialQuantity = 800, CurrentQuantity = 800, InitialAvgWeightGram = 30m, CurrentAvgWeightGram = 30m, UnitCost = 10m, Source = "Sonali Hatchery" },
            new() { PondId = ponds[1].Id, FishTypeId = fishTypes[3].Id, BatchCode = "B-P02-TIL", StockDate = start.AddDays(10), InitialQuantity = 3500, CurrentQuantity = 3500, InitialAvgWeightGram = 12m, CurrentAvgWeightGram = 12m, UnitCost = 4m, Source = "Local hatchery" },
            new() { PondId = ponds[2].Id, FishTypeId = fishTypes[4].Id, BatchCode = "B-P03-PAN", StockDate = start.AddDays(20), InitialQuantity = 5000, CurrentQuantity = 5000, InitialAvgWeightGram = 15m, CurrentAvgWeightGram = 15m, UnitCost = 5m, Source = "Sonali Hatchery" },
            new() { PondId = ponds[2].Id, FishTypeId = fishTypes[7].Id, BatchCode = "B-P03-SLV", StockDate = start.AddDays(20), InitialQuantity = 1200, CurrentQuantity = 1200, InitialAvgWeightGram = 20m, CurrentAvgWeightGram = 20m, UnitCost = 6m, Source = "Sonali Hatchery" },
        };

        db.FishStocks.AddRange(stocks);
        await db.SaveChangesAsync(ct);
        return stocks;
    }

    /// <summary>
    /// Walks day by day from <paramref name="start"/> writing feeding, growth samples, mortality,
    /// occasional treatments, sales and overheads, then reconciles every batch.
    /// </summary>
    private static async Task SeedDailyActivityAsync(
        AquaTrackDbContext db,
        List<Pond> ponds,
        List<FishStock> stocks,
        List<FeedItem> feedItems,
        List<Medicine> medicines,
        List<Customer> customers,
        DateOnly start,
        DateOnly today,
        CancellationToken ct)
    {
        var categories = await db.ExpenseCategories.ToDictionaryAsync(c => c.Name, c => c.Id, ct);
        var fishTypes = await db.FishTypes.ToDictionaryAsync(f => f.Id, f => f, ct);

        var feedUsages = new List<FeedUsage>();
        var growths = new List<FishGrowth>();
        var mortalities = new List<FishMortality>();
        var medicineUsages = new List<MedicineUsage>();
        var expenses = new List<Expense>();
        var cash = new List<CashTransaction>();
        var sales = new List<Sale>();

        var invoiceSeq = 0;

        for (var date = start; date <= today; date = date.AddDays(1))
        {
            foreach (var batch in stocks.Where(s => s.StockDate <= date))
            {
                var daysIn = date.DayNumber - batch.StockDate.DayNumber;
                var species = fishTypes[batch.FishTypeId];

                // Weight follows a gentle sigmoid-ish ramp toward the species target.
                var progress = Math.Min(1d, daysIn / (double)(species.GrowOutDays ?? 300));
                var target = (double)(species.TargetWeightGram ?? 1000m);
                var weight = (double)batch.InitialAvgWeightGram + (target - (double)batch.InitialAvgWeightGram) * progress;

                // Feed every day, sized from current biomass.
                var biomassKg = (decimal)weight * batch.CurrentQuantity / 1000m;
                var feedKg = Math.Round(biomassKg * 0.03m * (decimal)(0.9 + Rng.NextDouble() * 0.2), 2);

                if (feedKg > 0)
                {
                    var feed = feedItems[Rng.Next(feedItems.Count)];
                    feedUsages.Add(new FeedUsage
                    {
                        FeedItemId = feed.Id,
                        PondId = batch.PondId,
                        UsageDate = date,
                        Quantity = feedKg,
                        UnitCost = feed.AverageUnitCost,
                        TotalCost = Math.Round(feedKg * feed.AverageUnitCost, 2),
                    });
                }

                // Sample weight roughly every two weeks.
                if (daysIn > 0 && daysIn % 14 == 0)
                {
                    growths.Add(new FishGrowth
                    {
                        FishStockId = batch.Id,
                        RecordDate = date,
                        SampleSize = 10,
                        AvgWeightGram = Math.Round((decimal)weight * (decimal)(0.97 + Rng.NextDouble() * 0.06), 1),
                    });
                }

                // Background mortality on about one day in eight.
                if (Rng.NextDouble() < 0.12)
                {
                    var dead = Rng.Next(1, Math.Max(2, batch.InitialQuantity / 400));
                    mortalities.Add(new FishMortality
                    {
                        FishStockId = batch.Id,
                        RecordDate = date,
                        Quantity = dead,
                        Cause = (MortalityCause)(new[] { 1, 2, 3, 4, 99 })[Rng.Next(5)],
                        AvgWeightGram = (decimal)weight,
                        EstimatedLoss = Math.Round(dead * batch.UnitCost, 2),
                    });
                }
            }

            // A treatment round about once a fortnight.
            if (Rng.NextDouble() < 0.07)
            {
                var medicine = medicines[Rng.Next(medicines.Count)];
                var pond = ponds[Rng.Next(3)];
                var qty = Math.Round((decimal)(0.5 + Rng.NextDouble() * 3), 2);

                medicineUsages.Add(new MedicineUsage
                {
                    MedicineId = medicine.Id,
                    PondId = pond.Id,
                    UsageDate = date,
                    Quantity = qty,
                    UnitCost = medicine.AverageUnitCost,
                    TotalCost = Math.Round(qty * medicine.AverageUnitCost, 2),
                    Purpose = "Routine water treatment",
                });
            }

            // Harvest and sell roughly weekly, once the fish are big enough to be worth selling.
            if (date.DayNumber % 7 == 0 && date > start.AddMonths(2))
            {
                var batch = stocks[Rng.Next(stocks.Count)];
                var species = fishTypes[batch.FishTypeId];

                // Sell a slice of what the batch actually holds, so a run of random sales cannot
                // empty every pond before the demo period ends.
                var daysIn = date.DayNumber - batch.StockDate.DayNumber;
                var progress = Math.Min(1d, daysIn / (double)(species.GrowOutDays ?? 300));
                var target = (double)(species.TargetWeightGram ?? 1000m);
                var weightNow = (decimal)((double)batch.InitialAvgWeightGram
                                          + (target - (double)batch.InitialAvgWeightGram) * progress);

                var soldSoFar = sales
                    .SelectMany(s => s.Items)
                    .Where(i => i.FishStockId == batch.Id)
                    .Sum(i => i.QuantityKg);

                var availableKg = batch.InitialQuantity * weightNow / 1000m - soldSoFar;

                // Skip the sale rather than the whole day, so monthly overheads below still post.
                if (availableKg >= 60m)
                {
                    var qtyKg = Math.Round(
                        Math.Min(availableKg * 0.08m, (decimal)(40 + Rng.NextDouble() * 120)), 2);

                    var price = species.Name switch
                    {
                        "Rui" => 320m,
                        "Katla" => 300m,
                        "Tilapia" => 180m,
                        "Pangas" => 150m,
                        "Silver Carp" => 190m,
                        _ => 250m,
                    };

                    price = Math.Round(price * (decimal)(0.92 + Rng.NextDouble() * 0.16), 2);
                    var total = Math.Round(qtyKg * price, 2);
                    var customer = customers[Rng.Next(customers.Count)];
                    var paid = Rng.NextDouble() < 0.75 ? total : Math.Round(total * 0.6m, 2);

                    invoiceSeq++;
                    sales.Add(new Sale
                    {
                        InvoiceNo = $"INV-{date:yyyyMM}-{invoiceSeq:D4}",
                        CustomerId = customer.Id,
                        SaleDate = date,
                        SubTotal = total,
                        GrandTotal = total,
                        PaidAmount = paid,
                        PaymentMethod = paid >= total ? PaymentMethod.Cash : PaymentMethod.Credit,
                        Items =
                        [
                            new SaleItem
                            {
                                FishTypeId = batch.FishTypeId,
                                PondId = batch.PondId,
                                FishStockId = batch.Id,
                                QuantityKg = qtyKg,
                                UnitPrice = price,
                                LineTotal = total,
                            },
                        ],
                    });
                }
            }

            // Monthly overheads on the first of each month.
            if (date.Day == 1)
            {
                AddOverhead(expenses, cash, categories, ExpenseCategoryNames.Labour, date, Rng.Next(18000, 26000), "Monthly wages");
                AddOverhead(expenses, cash, categories, ExpenseCategoryNames.Electricity, date, Rng.Next(4000, 9000), "Electricity bill");
                AddOverhead(expenses, cash, categories, ExpenseCategoryNames.Transport, date, Rng.Next(2000, 6000), "Transport and fuel");
            }
        }

        db.FeedUsages.AddRange(feedUsages);
        db.FishGrowths.AddRange(growths);
        db.FishMortalities.AddRange(mortalities);
        db.MedicineUsages.AddRange(medicineUsages);
        db.Expenses.AddRange(expenses);
        db.Sales.AddRange(sales);
        await db.SaveChangesAsync(ct);

        // Feed and medicine consumed become expense lines and cash movements, grouped by month
        // so the cash book does not fill with hundreds of tiny rows.
        await PostConsumptionAsync(db, feedUsages, medicineUsages, categories, ct);
        await PostSalesCashAsync(db, sales, ct);

        db.CashTransactions.AddRange(cash);
        await db.SaveChangesAsync(ct);

        await ReconcileBatchesAsync(db, stocks, sales, ct);
    }

    private static void AddOverhead(
        List<Expense> expenses, List<CashTransaction> cash,
        Dictionary<string, int> categories, string category, DateOnly date, decimal amount, string description)
    {
        expenses.Add(new Expense
        {
            ExpenseCategoryId = categories[category],
            ExpenseDate = date,
            Amount = amount,
            PaymentMethod = PaymentMethod.Cash,
            Description = description,
            ReferenceType = ReferenceType.Expense,
            IsSystemGenerated = true,
        });

        cash.Add(new CashTransaction
        {
            TransactionDate = date,
            Direction = CashDirection.Out,
            Amount = amount,
            Category = category,
            PaymentMethod = PaymentMethod.Cash,
            Description = description,
            ReferenceType = ReferenceType.Expense,
            IsSystemGenerated = true,
        });
    }

    private static async Task PostConsumptionAsync(
        AquaTrackDbContext db,
        List<FeedUsage> feedUsages,
        List<MedicineUsage> medicineUsages,
        Dictionary<string, int> categories,
        CancellationToken ct)
    {
        var expenses = new List<Expense>();
        var cash = new List<CashTransaction>();

        foreach (var group in feedUsages.GroupBy(u => new { u.UsageDate.Year, u.UsageDate.Month }))
        {
            var date = new DateOnly(group.Key.Year, group.Key.Month, 1);
            var amount = Math.Round(group.Sum(u => u.TotalCost), 2);
            if (amount <= 0) continue;

            AddOverhead(expenses, cash, categories, ExpenseCategoryNames.Feed, date, amount,
                $"Feed consumed in {date:MMMM yyyy}");
        }

        foreach (var group in medicineUsages.GroupBy(u => new { u.UsageDate.Year, u.UsageDate.Month }))
        {
            var date = new DateOnly(group.Key.Year, group.Key.Month, 1);
            var amount = Math.Round(group.Sum(u => u.TotalCost), 2);
            if (amount <= 0) continue;

            AddOverhead(expenses, cash, categories, ExpenseCategoryNames.Medicine, date, amount,
                $"Medicine used in {date:MMMM yyyy}");
        }

        db.Expenses.AddRange(expenses);
        db.CashTransactions.AddRange(cash);
        await db.SaveChangesAsync(ct);
    }

    private static async Task PostSalesCashAsync(AquaTrackDbContext db, List<Sale> sales, CancellationToken ct)
    {
        var cash = sales
            .Where(s => s.PaidAmount > 0)
            .Select(s => new CashTransaction
            {
                TransactionDate = s.SaleDate,
                Direction = CashDirection.In,
                Amount = s.PaidAmount,
                Category = "Fish sale",
                PaymentMethod = s.PaymentMethod,
                Description = $"Sale {s.InvoiceNo}",
                ReferenceType = ReferenceType.Sale,
                ReferenceId = s.Id,
                CustomerId = s.CustomerId,
                IsSystemGenerated = true,
            })
            .ToList();

        db.CashTransactions.AddRange(cash);

        // Unpaid balances become customer receivables.
        foreach (var group in sales.Where(s => s.DueAmount > 0 && s.CustomerId != null).GroupBy(s => s.CustomerId!.Value))
        {
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == group.Key, ct);
            if (customer is not null)
                customer.ReceivableBalance += group.Sum(s => s.DueAmount);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Writes harvest rows for everything sold, then recomputes each batch's live figures.</summary>
    private static async Task ReconcileBatchesAsync(
        AquaTrackDbContext db, List<FishStock> stocks, List<Sale> sales, CancellationToken ct)
    {
        var harvests = new List<Harvest>();

        // Weight samples per batch, so a sale is converted to a head count using the weight the
        // fish actually were on that date. Using the stocking weight here would divide a sale by
        // a fingerling weight and appear to empty the pond in one go.
        var samples = await db.FishGrowths
            .GroupBy(g => g.FishStockId)
            .Select(g => new
            {
                FishStockId = g.Key,
                Points = g.OrderBy(x => x.RecordDate)
                    .Select(x => new { x.RecordDate, x.AvgWeightGram })
                    .ToList(),
            })
            .ToDictionaryAsync(x => x.FishStockId, x => x.Points, ct);

        foreach (var sale in sales)
        {
            foreach (var item in sale.Items.Where(i => i.FishStockId != null))
            {
                var batch = stocks.First(s => s.Id == item.FishStockId);

                var avgWeight = batch.InitialAvgWeightGram;
                if (samples.TryGetValue(batch.Id, out var points))
                {
                    var atSale = points.LastOrDefault(p => p.RecordDate <= sale.SaleDate);
                    if (atSale is not null) avgWeight = atSale.AvgWeightGram;
                }

                var pieces = avgWeight > 0 ? (int)Math.Round(item.QuantityKg * 1000m / avgWeight) : 0;

                harvests.Add(new Harvest
                {
                    FishStockId = batch.Id,
                    HarvestDate = sale.SaleDate,
                    Quantity = Math.Max(1, pieces),
                    TotalWeightKg = item.QuantityKg,
                    ExpectedUnitPrice = item.UnitPrice,
                    IsPartial = true,
                    Notes = $"Auto-recorded from sale {sale.InvoiceNo}",
                });
            }
        }

        db.Harvests.AddRange(harvests);
        await db.SaveChangesAsync(ct);

        foreach (var batch in stocks)
        {
            var dead = await db.FishMortalities.Where(m => m.FishStockId == batch.Id).SumAsync(m => (int?)m.Quantity, ct) ?? 0;
            var caught = await db.Harvests.Where(h => h.FishStockId == batch.Id).SumAsync(h => (int?)h.Quantity, ct) ?? 0;

            var latest = await db.FishGrowths
                .Where(g => g.FishStockId == batch.Id)
                .OrderByDescending(g => g.RecordDate)
                .FirstOrDefaultAsync(ct);

            batch.CurrentQuantity = Math.Max(0, batch.InitialQuantity - dead - caught);
            batch.MortalityCount = dead;
            batch.HarvestedCount = caught;
            batch.CurrentAvgWeightGram = latest?.AvgWeightGram ?? batch.InitialAvgWeightGram;

            if (batch.CurrentQuantity == 0)
                batch.Status = StockStatus.FullyHarvested;
        }

        await db.SaveChangesAsync(ct);
    }
}
