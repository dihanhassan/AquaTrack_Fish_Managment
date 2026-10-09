using AquaTrack.Domain.Common;

namespace AquaTrack.Domain.Entities;

public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Amount the farm still owes this supplier. Increased by credit purchases, reduced by payments.</summary>
    public decimal PayableBalance { get; set; }

    public ICollection<Purchase> Purchases { get; set; } = [];
}
