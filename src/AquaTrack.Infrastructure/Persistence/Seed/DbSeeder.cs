using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Identity;
using AquaTrack.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AquaTrack.Infrastructure.Persistence.Seed;

/// <summary>
/// Brings a fresh database up to a usable state: schema, roles, the first admin, the reference data
/// the UI needs to function, and (optionally) a demo dataset.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<AquaTrackDbContext>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbSeeder));
        var config = sp.GetRequiredService<IConfiguration>();

        await db.Database.MigrateAsync(ct);

        await SeedRolesAsync(sp, logger, ct);
        await SeedAdminAsync(sp, config, logger, ct);
        await SeedSettingsAsync(db, config, ct);
        await SeedExpenseCategoriesAsync(db, ct);
        await SeedFishTypesAsync(db, ct);

        if (config.GetValue("AquaTrack:SeedDemoData", false))
            await DemoDataSeeder.SeedAsync(db, logger, ct);
    }

    private static async Task SeedRolesAsync(IServiceProvider sp, ILogger logger, CancellationToken ct)
    {
        var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();

        foreach (var role in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(role)) continue;

            var result = await roleManager.CreateAsync(new ApplicationRole(role, Roles.Descriptions[role]));
            if (result.Succeeded)
                logger.LogInformation("Created role {Role}.", role);
            else
                logger.LogError("Could not create role {Role}: {Errors}", role,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private static async Task SeedAdminAsync(
        IServiceProvider sp, IConfiguration config, ILogger logger, CancellationToken ct)
    {
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var email = config["AquaTrack:Admin:Email"] ?? "admin@aquatrack.local";
        var password = config["AquaTrack:Admin:Password"] ?? "Admin#12345";
        var name = config["AquaTrack:Admin:FullName"] ?? "Farm Administrator";

        if (await userManager.FindByEmailAsync(email) is not null) return;

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = name,
            Designation = "Administrator",
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogError("Could not create the admin user: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, Roles.Admin);
        logger.LogWarning("Seeded admin account {Email}. Change this password after first sign-in.", email);
    }

    private static async Task SeedSettingsAsync(AquaTrackDbContext db, IConfiguration config, CancellationToken ct)
    {
        (string Key, string? Value, string Display, string Group, string? Description)[] defaults =
        [
            (SettingKeys.FarmName, config["AquaTrack:FarmName"] ?? "AquaTrack Fish Farm", "Farm name", "General", null),
            (SettingKeys.FarmOwner, null, "Owner name", "General", null),
            (SettingKeys.FarmAddress, null, "Address", "General", null),
            (SettingKeys.FarmPhone, null, "Phone", "General", null),
            (SettingKeys.CurrencyCode, "BDT", "Currency code", "Finance", "ISO code used on exported reports."),
            (SettingKeys.CurrencySymbol, "Tk", "Currency symbol", "Finance", "Shown beside amounts across the app."),
            (SettingKeys.OpeningCashBalance, "0", "Opening cash balance", "Finance",
                "Cash in hand before the first transaction was recorded."),
            (SettingKeys.LowStockWarningDays, "7", "Low stock warning days", "Alerts", null),
            (SettingKeys.MedicineExpiryWarningDays, "30", "Medicine expiry warning (days)", "Alerts", null),
            (SettingKeys.HighMortalityThresholdPercent, "2", "High mortality threshold (%)", "Alerts",
                "Weekly deaths above this share of the batch raise an alert."),
            (SettingKeys.CostSpikeSigma, "2", "Cost spike sensitivity (sigma)", "Alerts",
                "Standard deviations above the baseline before a cost spike is flagged."),
        ];

        var existing = await db.FarmSettings.Select(s => s.Key).ToListAsync(ct);

        var missing = defaults
            .Where(d => !existing.Contains(d.Key))
            .Select(d => new FarmSetting
            {
                Key = d.Key,
                Value = d.Value,
                DisplayName = d.Display,
                Group = d.Group,
                Description = d.Description,
            })
            .ToList();

        if (missing.Count == 0) return;

        db.FarmSettings.AddRange(missing);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedExpenseCategoriesAsync(AquaTrackDbContext db, CancellationToken ct)
    {
        // These names are referenced by the automatic posting rules, so they must always exist.
        (string Name, string Description)[] defaults =
        [
            (ExpenseCategories.Feed, "Fish feed purchased and consumed."),
            (ExpenseCategories.Medicine, "Medicines, probiotics and water treatments."),
            (ExpenseCategories.FishFry, "Fingerlings and fry purchased for stocking."),
            (ExpenseCategories.Labour, "Wages and daily labour."),
            (ExpenseCategories.Electricity, "Electricity and fuel bills."),
            (ExpenseCategories.Transport, "Transport and delivery costs."),
            (ExpenseCategories.Equipment, "Equipment and spare parts purchased."),
            (ExpenseCategories.Maintenance, "Repairs and servicing of equipment."),
            (ExpenseCategories.PondMaintenance, "Pond preparation, liming and excavation."),
            (ExpenseCategories.Other, "Anything that does not fit another category."),
        ];

        var existing = await db.ExpenseCategories.Select(c => c.Name).ToListAsync(ct);

        var missing = defaults
            .Where(d => !existing.Contains(d.Name))
            .Select(d => new ExpenseCategory { Name = d.Name, Description = d.Description, IsSystem = true })
            .ToList();

        if (missing.Count == 0) return;

        db.ExpenseCategories.AddRange(missing);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedFishTypesAsync(AquaTrackDbContext db, CancellationToken ct)
    {
        if (await db.FishTypes.AnyAsync(ct)) return;

        // Species commonly cultured in Bangladeshi polyculture ponds, with realistic grow-out figures.
        db.FishTypes.AddRange(
            new FishType { Name = "Rui", LocalName = "রুই", ScientificName = "Labeo rohita", TargetWeightGram = 1000, GrowOutDays = 300, DefaultFeedRatePercent = 3m },
            new FishType { Name = "Katla", LocalName = "কাতলা", ScientificName = "Catla catla", TargetWeightGram = 1200, GrowOutDays = 300, DefaultFeedRatePercent = 3m },
            new FishType { Name = "Mrigal", LocalName = "মৃগেল", ScientificName = "Cirrhinus cirrhosus", TargetWeightGram = 900, GrowOutDays = 300, DefaultFeedRatePercent = 3m },
            new FishType { Name = "Tilapia", LocalName = "তেলাপিয়া", ScientificName = "Oreochromis niloticus", TargetWeightGram = 500, GrowOutDays = 180, DefaultFeedRatePercent = 4m },
            new FishType { Name = "Pangas", LocalName = "পাঙাশ", ScientificName = "Pangasius pangasius", TargetWeightGram = 1200, GrowOutDays = 240, DefaultFeedRatePercent = 3.5m },
            new FishType { Name = "Koi", LocalName = "কই", ScientificName = "Anabas testudineus", TargetWeightGram = 120, GrowOutDays = 150, DefaultFeedRatePercent = 5m },
            new FishType { Name = "Shing", LocalName = "শিং", ScientificName = "Heteropneustes fossilis", TargetWeightGram = 100, GrowOutDays = 180, DefaultFeedRatePercent = 5m },
            new FishType { Name = "Silver Carp", LocalName = "সিলভার কার্প", ScientificName = "Hypophthalmichthys molitrix", TargetWeightGram = 1500, GrowOutDays = 300, DefaultFeedRatePercent = 2.5m },
            new FishType { Name = "Grass Carp", LocalName = "গ্রাস কার্প", ScientificName = "Ctenopharyngodon idella", TargetWeightGram = 1500, GrowOutDays = 330, DefaultFeedRatePercent = 2.5m },
            new FishType { Name = "Common Carp", LocalName = "কমন কার্প", ScientificName = "Cyprinus carpio", TargetWeightGram = 1000, GrowOutDays = 300, DefaultFeedRatePercent = 3m });

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Names of the seeded expense categories, used by the automatic posting rules.</summary>
public static class ExpenseCategories
{
    public const string Feed = "Feed";
    public const string Medicine = "Medicine";
    public const string FishFry = "Fish Fry";
    public const string Labour = "Labour";
    public const string Electricity = "Electricity";
    public const string Transport = "Transport";
    public const string Equipment = "Equipment";
    public const string Maintenance = "Maintenance";
    public const string PondMaintenance = "Pond Maintenance";
    public const string Other = "Other";
}
