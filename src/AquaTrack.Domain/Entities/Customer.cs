using AquaTrack.Domain.Common;

namespace AquaTrack.Domain.Entities;

public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Outstanding amount owed to the farm by this customer.</summary>
    public decimal ReceivableBalance { get; set; }

    public ICollection<Sale> Sales { get; set; } = [];
}
