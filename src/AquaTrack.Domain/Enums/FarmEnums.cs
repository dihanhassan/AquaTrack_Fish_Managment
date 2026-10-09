namespace AquaTrack.Domain.Enums;

public enum WaterType { Freshwater = 1, Brackish = 2, Saline = 3 }

public enum PondStatus { Active = 1, Inactive = 2, UnderMaintenance = 3, Drying = 4 }

public enum StockStatus { Active = 1, FullyHarvested = 2, Lost = 3 }

public enum MortalityCause
{
    Unknown = 0, Disease = 1, OxygenDepletion = 2, WaterQuality = 3,
    Predation = 4, Handling = 5, Weather = 6, Poisoning = 7, Other = 99
}

public enum StockAdjustmentReason
{
    PhysicalCount = 1, Transfer = 2, DataCorrection = 3, Escape = 4, Theft = 5, Other = 99
}
