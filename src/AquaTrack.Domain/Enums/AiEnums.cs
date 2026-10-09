namespace AquaTrack.Domain.Enums;

public enum RecommendationType
{
    FeedRecommendation = 1, GrowthPrediction = 2, ExpensePrediction = 3,
    ProfitPrediction = 4, AnomalyAlert = 5, StockAlert = 6, HarvestAdvice = 7
}

public enum RecommendationStatus { New = 1, Acknowledged = 2, Applied = 3, Dismissed = 4 }

public enum NotificationSeverity { Info = 1, Success = 2, Warning = 3, Critical = 4 }

public enum NotificationType
{
    LowStock = 1, ExpiringMedicine = 2, ExpiredMedicine = 3, HighMortality = 4,
    CostSpike = 5, MaintenanceDue = 6, DuePayment = 7, System = 99
}
