namespace AquaTrack.Domain.Enums;

public enum PaymentMethod { Cash = 1, BankTransfer = 2, MobileBanking = 3, Cheque = 4, Credit = 5, Other = 99 }

public enum CashDirection { In = 1, Out = 2 }

/// <summary>What business document produced a ledger row, so cash can be traced back to its source.</summary>
public enum ReferenceType
{
    Manual = 0, Sale = 1, Purchase = 2, Expense = 3, FeedPurchase = 4,
    MedicinePurchase = 5, EquipmentPurchase = 6, Maintenance = 7,
    CustomerPayment = 8, SupplierPayment = 9, OtherIncome = 10, OpeningBalance = 11
}

public enum PurchaseItemType { FishFry = 1, Feed = 2, Medicine = 3, Equipment = 4, EquipmentPart = 5, Other = 99 }

public enum PartyType { Supplier = 1, Customer = 2, Both = 3 }
