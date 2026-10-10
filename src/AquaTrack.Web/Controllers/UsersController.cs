using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Infrastructure.Identity;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// User administration. Guards against the two ways an admin can lock everyone out: removing the
/// last administrator, and deactivating or deleting their own account.
/// </summary>
[Authorize(Policy = Policies.Administer)]
public class UsersController(
    UserManager<ApplicationUser> userManager,
    ICurrentUser currentUser) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Users";
        ViewData["Subtitle"] = "Who can sign in, and what they can do";

        return View(await BuildListAsync(ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => PartialView("_Table", await BuildListAsync(ct));

    private async Task<UserListViewModel> BuildListAsync(CancellationToken ct)
    {
        var users = await userManager.Users
            .OrderBy(u => u.FullName)
            .AsNoTracking()
            .ToListAsync(ct);

        var rows = new List<UserRow>(users.Count);

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);

            rows.Add(new UserRow
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Phone = user.PhoneNumber,
                Designation = user.Designation,
                IsActive = user.IsActive,
                LastLoginUtc = user.LastLoginUtc,
                Roles = [.. roles],
                IsSelf = user.UserName == currentUser.UserName,
            });
        }

        return new UserListViewModel { Users = rows };
    }

    [HttpGet]
    public async Task<IActionResult> Form(string? id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(id))
            return PartialView("_Form", new UserFormViewModel { Role = Roles.Worker });

        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var roles = await userManager.GetRolesAsync(user);

        return PartialView("_Form", new UserFormViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Designation = user.Designation,
            Role = roles.FirstOrDefault() ?? Roles.Worker,
            IsActive = user.IsActive,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(UserFormViewModel model, CancellationToken ct)
    {
        if (model.Id is null && string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Set a password for the new user.");

        var existing = await userManager.FindByEmailAsync(model.Email);
        if (existing is not null && existing.Id != model.Id)
            ModelState.AddModelError(nameof(model.Email), "Another user already uses this email address.");

        if (!ModelState.IsValid)
            return Invalid(model);

        if (model.Id is null)
            return await CreateAsync(model);

        return await UpdateAsync(model);
    }

    private async Task<IActionResult> CreateAsync(UserFormViewModel model)
    {
        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            Designation = model.Designation,
            IsActive = model.IsActive,
        };

        var result = await userManager.CreateAsync(user, model.Password!);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return Invalid(model);
        }

        await userManager.AddToRoleAsync(user, model.Role);
        return Saved($"{user.FullName} can now sign in as {model.Role}.");
    }

    private async Task<IActionResult> UpdateAsync(UserFormViewModel model)
    {
        var user = await userManager.FindByIdAsync(model.Id!);
        if (user is null) return NotFound();

        var isSelf = user.UserName == currentUser.UserName;

        // Locking yourself out is the one mistake that cannot be undone from inside the app.
        if (isSelf && !model.IsActive)
        {
            ModelState.AddModelError(string.Empty, "You cannot deactivate your own account.");
            return Invalid(model);
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var wasAdmin = currentRoles.Contains(Roles.Admin);

        if (wasAdmin && model.Role != Roles.Admin && await IsLastAdminAsync(user.Id))
        {
            ModelState.AddModelError(string.Empty,
                "This is the only administrator. Promote someone else before changing this role.");
            return Invalid(model);
        }

        user.FullName = model.FullName;
        user.Email = model.Email;
        user.UserName = model.Email;
        user.PhoneNumber = model.PhoneNumber;
        user.Designation = model.Designation;
        user.IsActive = model.IsActive;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return Invalid(model);
        }

        if (!currentRoles.Contains(model.Role))
        {
            await userManager.RemoveFromRolesAsync(user, currentRoles);
            await userManager.AddToRoleAsync(user, model.Role);
        }

        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await userManager.ResetPasswordAsync(user, token, model.Password);

            if (!reset.Succeeded)
            {
                AddIdentityErrors(reset);
                return Invalid(model);
            }
        }

        return Saved($"{user.FullName} updated.");
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmDelete(string id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        return PartialView("_Confirm", new ConfirmViewModel
        {
            Title = "Delete user",
            Message = $"Delete {user.FullName}?",
            Detail = "Records they entered stay in the system, attributed to their name.",
            Action = Url.Action(nameof(Delete), new { id })!,
        });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (user.UserName == currentUser.UserName)
            return Failed("You cannot delete your own account.");

        if (await IsLastAdminAsync(user.Id))
            return Failed("This is the only administrator and cannot be deleted.");

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return Failed(string.Join(" ", result.Errors.Select(e => e.Description)));

        return Saved($"{user.FullName} deleted.");
    }

    /// <summary>True when the given user is the only active administrator left.</summary>
    private async Task<bool> IsLastAdminAsync(string userId)
    {
        var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
        return admins.Count(a => a.IsActive) <= 1 && admins.Any(a => a.Id == userId);
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
    }

    private IActionResult Invalid(UserFormViewModel model)
    {
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return PartialView("_Form", model);
    }
}
