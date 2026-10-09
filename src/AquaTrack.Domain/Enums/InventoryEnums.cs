namespace AquaTrack.Domain.Enums;

public enum FeedType
{
    Floating = 1, Sinking = 2, Powder = 3, Starter = 4,
    Grower = 5, Finisher = 6, Supplement = 7, Other = 99
}

public enum MedicineType
{
    Antibiotic = 1, Antifungal = 2, Probiotic = 3, Disinfectant = 4,
    Vitamin = 5, Mineral = 6, Lime = 7, WaterConditioner = 8, Other = 99
}

public enum EquipmentCategory
{
    WaterPump = 1, Aerator = 2, FishingNet = 3, OxygenMachine = 4,
    WaterTestingKit = 5, Pipe = 6, Motor = 7, ElectricalPart = 8,
    Generator = 9, Vehicle = 10, SparePart = 11, Other = 99
}

public enum EquipmentCondition { New = 1, Good = 2, Fair = 3, NeedsRepair = 4, OutOfService = 5, Disposed = 6 }

public enum MaintenanceType { Routine = 1, Repair = 2, Replacement = 3, Inspection = 4 }

public enum UnitOfMeasure { Kg = 1, Gram = 2, Litre = 3, Millilitre = 4, Piece = 5, Packet = 6, Bag = 7, Bottle = 8, Metre = 9 }
