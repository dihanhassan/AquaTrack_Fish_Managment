using AquaTrack.Domain.Entities;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AquaTrack.Infrastructure.Services;

public static class SettingKeys
{
    public const string FarmName = "Farm.Name";
    public const string FarmOwner = "Farm.Owner";
    public const string FarmAddress = "Farm.Address";
    public const string FarmPhone = "Farm.Phone";
    public const string CurrencyCode = "Finance.CurrencyCode";
    public const string CurrencySymbol = "Finance.CurrencySymbol";
    public const string OpeningCashBalance = "Finance.OpeningCashBalance";
    public const string LowStockWarningDays = "Alerts.LowStockWarningDays";
    public const string MedicineExpiryWarningDays = "Alerts.MedicineExpiryWarningDays";
    public const string HighMortalityThresholdPercent = "Alerts.HighMortalityThresholdPercent";
    public const string CostSpikeSigma = "Alerts.CostSpikeSigma";
}

public interface ISettingsService
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task<string> GetAsync(string key, string fallback, CancellationToken ct = default);
    Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
    Task SetAsync(string key, string? value, CancellationToken ct = default);
    Task<IReadOnlyList<FarmSetting>> GetAllAsync(CancellationToken ct = default);
}

/// <summary>Settings are read on nearly every request, so they are cached until something writes one.</summary>
public class SettingsService(AquaTrackDbContext db, IMemoryCache cache) : ISettingsService
{
    private const string CacheKey = "aquatrack.settings";

    private async Task<Dictionary<string, string?>> LoadAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out Dictionary<string, string?>? cached) && cached is not null)
            return cached;

        var map = await db.FarmSettings.AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        cache.Set(CacheKey, map, TimeSpan.FromMinutes(30));
        return map;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
        => (await LoadAsync(ct)).GetValueOrDefault(key);

    public async Task<string> GetAsync(string key, string fallback, CancellationToken ct = default)
    {
        var value = await GetAsync(key, ct);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default)
        => decimal.TryParse(await GetAsync(key, ct), out var value) ? value : fallback;

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
        => int.TryParse(await GetAsync(key, ct), out var value) ? value : fallback;

    public async Task SetAsync(string key, string? value, CancellationToken ct = default)
    {
        var setting = await db.FarmSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
        {
            setting = new FarmSetting { Key = key, Value = value };
            db.FarmSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
        }

        await db.SaveChangesAsync(ct);
        cache.Remove(CacheKey);
    }

    public async Task<IReadOnlyList<FarmSetting>> GetAllAsync(CancellationToken ct = default)
        => await db.FarmSettings.AsNoTracking().OrderBy(s => s.Group).ThenBy(s => s.Key).ToListAsync(ct);
}
