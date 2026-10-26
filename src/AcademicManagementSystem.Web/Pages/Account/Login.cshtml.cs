using System.ComponentModel.DataAnnotations;
using AcademicManagementSystem.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicManagementSystem.Web.Pages.Account;

[AllowAnonymous]
public class LoginModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return await RedirectForRoleAsync();
        }

        if ((await userManager.GetUsersInRoleAsync("Administrator")).Count == 0)
        {
            return RedirectToPage("/Setup");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var loginIdentifier = Input.LoginIdentifier.Trim();

        // Students: Student ID is their UserName.
        // Administrators and Coordinators: email address is their UserName/email.
        var user = await userManager.FindByNameAsync(loginIdentifier)
                   ?? await userManager.FindByEmailAsync(loginIdentifier);

        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(
                string.Empty,
                "Invalid Student ID/email address or password.");

            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.IsLockedOut
                    ? "This account is temporarily locked. Contact an administrator."
                    : "Invalid Student ID/email address or password.");

            return Page();
        }

        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        return await RedirectForRoleAsync(user);
    }

    private async Task<IActionResult> RedirectForRoleAsync(ApplicationUser? knownUser = null)
    {
        var user = knownUser ?? await userManager.GetUserAsync(User);

        if (user is not null && await userManager.IsInRoleAsync(user, "Administrator"))
        {
            return RedirectToPage("/Admin/Index");
        }

        if (user is not null && await userManager.IsInRoleAsync(user, "Coordinator"))
        {
            return RedirectToPage("/Coordinator/Index");
        }

        return RedirectToPage("/Student/Index");
    }

    public sealed class LoginInput
    {
        [Required]
        [Display(Name = "Student ID or email address")]
        public string LoginIdentifier { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}