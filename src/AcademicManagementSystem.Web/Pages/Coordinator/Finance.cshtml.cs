using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicManagementSystem.Web.Pages.Coordinator;

[Authorize(Roles = "Administrator")]
public class FinanceModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Admin/Finance");
}
