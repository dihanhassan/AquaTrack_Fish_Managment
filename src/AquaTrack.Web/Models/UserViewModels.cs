using System.ComponentModel.DataAnnotations;
using AquaTrack.Infrastructure.Identity;

namespace AquaTrack.Web.Models;

public class UserRow
{
    public string Id { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Designation { get; init; }
    public bool IsActive { get; init; }
    public DateTime? LastLoginUtc { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>True for the signed-in user, whose own account is protected from self-lockout.</summary>
    public bool IsSelf { get; init; }

    public string PrimaryRole => Roles.Count > 0 ? Roles[0] : "No role";

    // Qualified, because the Roles property above shadows the Roles constants class here.
    public string RoleTone => PrimaryRole switch
    {
        AquaTrack.Infrastructure.Identity.Roles.Admin => "danger",
        AquaTrack.Infrastructure.Identity.Roles.Manager => "info",
        AquaTrack.Infrastructure.Identity.Roles.Worker => "neutral",
        _ => "neutral",
    };
}

public class UserListViewModel
{
    public IReadOnlyList<UserRow> Users { get; init; } = [];

    public int ActiveCount => Users.Count(u => u.IsActive);
    public int AdminCount => Users.Count(u => u.Roles.Contains(Roles.Admin) && u.IsActive);
}

public class UserFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Enter the person's full name.")]
    [StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter an email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    [Display(Name = "Email (used to sign in)")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(40)]
    [Display(Name = "Phone")]
    public string? PhoneNumber { get; set; }

    [StringLength(100)]
    [Display(Name = "Designation")]
    public string? Designation { get; set; }

    [Required]
    [Display(Name = "Role")]
    public string Role { get; set; } = Roles.Worker;

    /// <summary>Required when creating; on an edit, leaving it blank keeps the existing password.</summary>
    [StringLength(100, MinimumLength = 8, ErrorMessage = "The password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Display(Name = "Can sign in")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => Id is not null;

    public static IReadOnlyList<(string Name, string Description)> RoleChoices =>
        [.. Roles.All.Select(role => (role, Roles.Descriptions[role]))];
}
