using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class IndexModel(
    AcademicManagementDbContext database,
    AcademicSettingsService settingsService) : PageModel
{
    public AcademicSettings Settings { get; private set; } = new();
    public int ProgrammeCount { get; private set; }
    public int StudyModeCount { get; private set; }
    public int CourseCount { get; private set; }
    public int StudentCount { get; private set; }
    public int InvoiceCount { get; private set; }
    public int TimetableEntryCount { get; private set; }

    public async Task OnGetAsync()
    {
        Settings = await settingsService.GetAsync();
        ProgrammeCount = await database.Programmes.CountAsync();
        StudyModeCount = await database.StudyModes.CountAsync();
        CourseCount = await database.Courses.CountAsync();
        StudentCount = await database.Students.CountAsync();
        InvoiceCount = await database.Invoices.CountAsync();
        TimetableEntryCount = await database.TimetableEntries.CountAsync();
    }
}
