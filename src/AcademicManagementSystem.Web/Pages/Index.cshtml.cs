using AcademicManagementSystem.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicManagementSystem.Web.Pages;

[AllowAnonymous]
public class IndexModel(UserManager<ApplicationUser> userManager) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        if ((await userManager.GetUsersInRoleAsync("Administrator")).Count == 0)
        {
            return RedirectToPage("/Setup");
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login");
        }

        var user = await userManager.GetUserAsync(User);
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
}
