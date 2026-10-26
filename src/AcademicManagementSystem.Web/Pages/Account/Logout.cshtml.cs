using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AcademicManagementSystem.Web.Models;

namespace AcademicManagementSystem.Web.Pages.Account;

[Authorize]
public class LogoutModel(SignInManager<ApplicationUser> signInManager) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await signInManager.SignOutAsync();
        return RedirectToPage("/Account/Login");
    }
}
