using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicManagementSystem.Web.Pages;

[Authorize(Roles = "Student")]
public class ResultsModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Student/Results");
}
