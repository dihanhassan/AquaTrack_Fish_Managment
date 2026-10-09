namespace AquaTrack.Application.Common.Models;

/// <summary>
/// The expense categories the automatic posting rules depend on. They are seeded on first run and
/// marked as system categories so they cannot be deleted out from under those rules.
/// </summary>
public static class ExpenseCategoryNames
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

    public static readonly (string Name, string Description)[] Defaults =
    [
        (Feed, "Fish feed purchased and consumed."),
        (Medicine, "Medicines, probiotics and water treatments."),
        (FishFry, "Fingerlings and fry purchased for stocking."),
        (Labour, "Wages and daily labour."),
        (Electricity, "Electricity and fuel bills."),
        (Transport, "Transport and delivery costs."),
        (Equipment, "Equipment and spare parts purchased."),
        (Maintenance, "Repairs and servicing of equipment."),
        (PondMaintenance, "Pond preparation, liming and excavation."),
        (Other, "Anything that does not fit another category."),
    ];
}
