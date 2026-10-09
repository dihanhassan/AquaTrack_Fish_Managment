using System.ComponentModel.DataAnnotations;
using AquaTrack.Web.Infrastructure;

namespace AquaTrack.Web.Models;

/// <summary>Shared list shape for suppliers and customers, which differ only in their columns.</summary>
public class PartyListViewModel<T>
{
    public required PagedList<T> Page { get; init; }
    public required ListQuery Query { get; init; }
    public bool OnlyWithDues { get; init; }

    public IDictionary<string, string?> RouteValues()
    {
        var values = Query.ToRouteValues();
        if (OnlyWithDues) values["dues"] = "true";
        return values;
    }
}

public class SupplierFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Enter the supplier name.")]
    [StringLength(150)]
    [Display(Name = "Supplier name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Contact person")]
    public string? ContactPerson { get; set; }

    [StringLength(40)]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(400)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => Id.HasValue;
}

public class CustomerFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Enter the customer name.")]
    [StringLength(150)]
    [Display(Name = "Customer name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(40)]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(400)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => Id.HasValue;
}
