using System.ComponentModel.DataAnnotations;
using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class UsersModel(
    UserManager<ApplicationUser> userManager,
    AcademicManagementDbContext database) : PageModel
{
    [BindProperty]
    public StaffAccountInput NewAccount { get; set; } = new();

    public IReadOnlyList<UserRow> Users { get; private set; } = [];

    public IReadOnlyList<School> Schools { get; private set; } = [];

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

        School? coordinatingSchool = null;

        if (NewAccount.Role == "Coordinator")
        {
            if (NewAccount.CoordinatingSchoolId <= 0)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Choose a school for the coordinator.");

                await LoadPageAsync();
                return Page();
            }

            coordinatingSchool = await database.Schools
                .SingleOrDefaultAsync(school =>
                    school.Id == NewAccount.CoordinatingSchoolId &&
                    school.IsActive);

            if (coordinatingSchool is null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Choose a valid active school for the coordinator.");

                await LoadPageAsync();
                return Page();
            }
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
            EmailConfirmed = true,
            CoordinatingSchoolId = coordinatingSchool?.Id
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

        TempData["Success"] = NewAccount.Role == "Coordinator"
            ? $"Coordinator account created for {user.FullName} in {coordinatingSchool!.Name}."
            : $"Administrator account created for {user.FullName}.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAssignCoordinatorSchoolAsync(
        string userId,
        int? coordinatingSchoolId)
    {
        var user = await userManager.FindByIdAsync(userId);

        if (user is null)
        {
            TempData["Error"] = "Account not found.";
            return RedirectToPage();
        }

        if (!await userManager.IsInRoleAsync(user, "Coordinator"))
        {
            TempData["Error"] = "Only coordinator accounts can be assigned to a school.";
            return RedirectToPage();
        }

        if (coordinatingSchoolId is null or <= 0)
        {
            user.CoordinatingSchoolId = null;

            var clearResult = await userManager.UpdateAsync(user);

            TempData["Success"] = clearResult.Succeeded
                ? $"School assignment was removed from {user.FullName}."
                : "The coordinator school assignment could not be updated.";

            return RedirectToPage();
        }

        var school = await database.Schools.SingleOrDefaultAsync(item =>
            item.Id == coordinatingSchoolId.Value && item.IsActive);

        if (school is null)
        {
            TempData["Error"] = "Choose a valid active school.";
            return RedirectToPage();
        }

        user.CoordinatingSchoolId = school.Id;

        var updateResult = await userManager.UpdateAsync(user);

        TempData["Success"] = updateResult.Succeeded
            ? $"{user.FullName} was assigned to {school.Name}."
            : "The coordinator school assignment could not be updated.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(
        string userId,
        bool isActive)
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
            TempData["Error"] =
                "You cannot disable the account currently being used.";

            return RedirectToPage();
        }

        user.IsActive = isActive;
        await userManager.UpdateAsync(user);

        TempData["Success"] =
            $"{user.FullName}'s account was {(isActive ? "enabled" : "disabled")}.";

        return RedirectToPage();
    }

    private async Task LoadPageAsync()
    {
        Schools = await database.Schools
            .Where(school => school.IsActive)
            .AsNoTracking()
            .OrderBy(school => school.Name)
            .ToListAsync();

        var schoolNames = Schools.ToDictionary(
            school => school.Id,
            school => school.Name);

        var accounts = await userManager.Users
            .OrderBy(user => user.Email)
            .ToListAsync();

        var rows = new List<UserRow>();

        foreach (var account in accounts)
        {
            var roles = await userManager.GetRolesAsync(account);

            var schoolName =
                account.CoordinatingSchoolId.HasValue &&
                schoolNames.TryGetValue(
                    account.CoordinatingSchoolId.Value,
                    out var foundSchoolName)
                    ? foundSchoolName
                    : null;

            rows.Add(new UserRow(
                account.Id,
                account.FullName,
                account.Email ?? account.UserName ?? "No email",
                string.Join(", ", roles),
                account.StudentId,
                account.CoordinatingSchoolId,
                schoolName,
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

        public int CoordinatingSchoolId { get; set; }
    }

    public sealed record UserRow(
        string Id,
        string FullName,
        string Email,
        string Roles,
        int? StudentId,
        int? CoordinatingSchoolId,
        string? CoordinatingSchoolName,
        bool IsActive);
}