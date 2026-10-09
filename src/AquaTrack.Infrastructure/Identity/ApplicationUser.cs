using Microsoft.AspNetCore.Identity;

namespace AquaTrack.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginUtc { get; set; }
}

public class ApplicationRole : IdentityRole
{
    public string? Description { get; set; }

    public ApplicationRole() { }
    public ApplicationRole(string name, string? description = null) : base(name) => Description = description;
}

/// <summary>The three roles from the spec. Used for <c>[Authorize(Roles = ...)]</c> on controllers.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Worker = "Worker";

    /// <summary>Admin and Manager — everything except user administration.</summary>
    public const string AdminOrManager = $"{Admin},{Manager}";
    /// <summary>Anyone signed in; Workers may record daily operational data.</summary>
    public const string AllStaff = $"{Admin},{Manager},{Worker}";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [Admin] = "Full control over the system, including users and settings.",
        [Manager] = "Manages farm operations, stock, purchases, sales, expenses and reports.",
        [Worker] = "Records daily feeding, medicine usage, mortality and growth data.",
    };

    public static readonly string[] All = [Admin, Manager, Worker];
}
