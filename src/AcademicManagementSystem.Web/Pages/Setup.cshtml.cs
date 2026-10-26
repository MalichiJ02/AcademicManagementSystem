using System.ComponentModel.DataAnnotations;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicManagementSystem.Web.Pages;

[AllowAnonymous]
public class SetupModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    AcademicSettingsService settingsService) : PageModel
{
    [BindProperty]
    public SetupInput Input { get; set; } = new();

    public bool HasAdministrator { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        HasAdministrator = (await userManager.GetUsersInRoleAsync("Administrator")).Count > 0;
        if (HasAdministrator)
        {
            return RedirectToPage("/Account/Login");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        HasAdministrator = (await userManager.GetUsersInRoleAsync("Administrator")).Count > 0;
        if (HasAdministrator)
        {
            return RedirectToPage("/Account/Login");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = Input.Email.Trim(),
            Email = Input.Email.Trim(),
            FullName = Input.FullName.Trim(),
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        await userManager.AddToRoleAsync(user, "Administrator");
        await settingsService.GetAsync();
        await signInManager.SignInAsync(user, isPersistent: false);
        TempData["Success"] = "Administrator account created. Configure your academic settings before adding data.";
        return RedirectToPage("/Admin/Index");
    }

    public sealed class SetupInput
    {
        [Required, StringLength(120)]
        [Display(Name = "Administrator name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
