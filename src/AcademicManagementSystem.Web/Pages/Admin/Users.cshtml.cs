using System.ComponentModel.DataAnnotations;
using AcademicManagementSystem.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class UsersModel(UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public StaffAccountInput NewAccount { get; set; } = new();

    public IReadOnlyList<UserRow> Users { get; private set; } = [];

    public async Task OnGetAsync() => await LoadPageAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAccount.FullName) ||
            !new EmailAddressAttribute().IsValid(NewAccount.Email) ||
            NewAccount.Password.Length < 8 ||
            NewAccount.Role is not ("Administrator" or "Coordinator"))
        {
            ModelState.AddModelError(
                string.Empty,
                "Complete a name, valid email, 8-character password, and Administrator or Coordinator role.");
            await LoadPageAsync();
            return Page();
        }

        if (await userManager.FindByEmailAsync(NewAccount.Email.Trim()) is not null)
        {
            ModelState.AddModelError(string.Empty, "That email address is already in use.");
            await LoadPageAsync();
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = NewAccount.Email.Trim(),
            Email = NewAccount.Email.Trim(),
            FullName = NewAccount.FullName.Trim(),
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(user, NewAccount.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await LoadPageAsync();
            return Page();
        }

        var roleResult = await userManager.AddToRoleAsync(user, NewAccount.Role);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await LoadPageAsync();
            return Page();
        }

        TempData["Success"] = $"{NewAccount.Role} account created for {user.FullName}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(string userId, bool isActive)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            TempData["Error"] = "Account not found.";
            return RedirectToPage();
        }

        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser?.Id == user.Id && !isActive)
        {
            TempData["Error"] = "You cannot disable the account currently being used.";
            return RedirectToPage();
        }

        user.IsActive = isActive;
        await userManager.UpdateAsync(user);
        TempData["Success"] = $"{user.FullName}'s account was {(isActive ? "enabled" : "disabled")}.";
        return RedirectToPage();
    }

    private async Task LoadPageAsync()
    {
        var accounts = await userManager.Users
            .OrderBy(user => user.Email)
            .ToListAsync();
        var rows = new List<UserRow>();
        foreach (var account in accounts)
        {
            var roles = await userManager.GetRolesAsync(account);
            rows.Add(new UserRow(
                account.Id,
                account.FullName,
                account.Email ?? account.UserName ?? "No email",
                string.Join(", ", roles),
                account.StudentId,
                account.IsActive));
        }
        Users = rows;
    }

    public sealed class StaffAccountInput
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Coordinator";
    }

    public sealed record UserRow(
        string Id,
        string FullName,
        string Email,
        string Roles,
        int? StudentId,
        bool IsActive);
}
