using AquaTrack.Application.Services;
using AquaTrack.Web.Infrastructure;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// Farm configuration. Settings are stored as key/value pairs and read on nearly every request, so
/// the service caches them and this controller invalidates that cache by writing through it.
/// </summary>
[Authorize(Policy = Policies.Administer)]
public class SettingsController(ISettingsService settings) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Settings";
        ViewData["Subtitle"] = "Farm details, currency and alert thresholds";

        return View(await BuildAsync(ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SettingsViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Form", await BuildAsync(ct));
        }

        await settings.SetAsync(SettingKeys.FarmName, model.FarmName.Trim(), ct);
        await settings.SetAsync(SettingKeys.FarmOwner, model.FarmOwner, ct);
        await settings.SetAsync(SettingKeys.FarmAddress, model.FarmAddress, ct);
        await settings.SetAsync(SettingKeys.FarmPhone, model.FarmPhone, ct);

        await settings.SetAsync(SettingKeys.CurrencyCode, model.CurrencyCode.Trim(), ct);
        await settings.SetAsync(SettingKeys.CurrencySymbol, model.CurrencySymbol.Trim(), ct);
        await settings.SetAsync(SettingKeys.OpeningCashBalance, model.OpeningCashBalance.ToString("F2"), ct);

        await settings.SetAsync(SettingKeys.LowStockWarningDays, model.LowStockWarningDays.ToString(), ct);
        await settings.SetAsync(SettingKeys.MedicineExpiryWarningDays, model.MedicineExpiryWarningDays.ToString(), ct);
        await settings.SetAsync(SettingKeys.HighMortalityThresholdPercent,
            model.HighMortalityThresholdPercent.ToString("F2"), ct);
        await settings.SetAsync(SettingKeys.CostSpikeSigma, model.CostSpikeSigma.ToString("F2"), ct);

        Response.HxToast("Settings saved.");
        return PartialView("_Form", await BuildAsync(ct));
    }

    private async Task<SettingsViewModel> BuildAsync(CancellationToken ct) => new()
    {
        FarmName = await settings.GetAsync(SettingKeys.FarmName, "AquaTrack Fish Farm", ct),
        FarmOwner = await settings.GetAsync(SettingKeys.FarmOwner, ct),
        FarmAddress = await settings.GetAsync(SettingKeys.FarmAddress, ct),
        FarmPhone = await settings.GetAsync(SettingKeys.FarmPhone, ct),

        CurrencyCode = await settings.GetAsync(SettingKeys.CurrencyCode, "BDT", ct),
        CurrencySymbol = await settings.GetAsync(SettingKeys.CurrencySymbol, "Tk", ct),
        OpeningCashBalance = await settings.GetDecimalAsync(SettingKeys.OpeningCashBalance, 0m, ct),

        LowStockWarningDays = await settings.GetIntAsync(SettingKeys.LowStockWarningDays, 7, ct),
        MedicineExpiryWarningDays = await settings.GetIntAsync(SettingKeys.MedicineExpiryWarningDays, 30, ct),
        HighMortalityThresholdPercent =
            await settings.GetDecimalAsync(SettingKeys.HighMortalityThresholdPercent, 2m, ct),
        CostSpikeSigma = await settings.GetDecimalAsync(SettingKeys.CostSpikeSigma, 2m, ct),
    };
}
