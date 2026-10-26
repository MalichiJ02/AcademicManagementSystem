using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.StudentPortal;

[Authorize(Roles = "Student")]
public class TimetableModel(
    AcademicManagementDbContext database,
    CurrentStudentService currentStudentService,
    AcademicSettingsService settingsService) : PageModel
{
    public AcademicSettings Settings { get; private set; } = new();
    public IReadOnlyList<TimetableEntry> Entries { get; private set; } = [];
    public IReadOnlyList<CourseRegistration> RegistrationsWithoutTimetable { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var currentStudent = await currentStudentService.GetStudentAsync(User);
        if (currentStudent is null || !currentStudent.IsActive)
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        Settings = await settingsService.GetAsync();
        var registrations = await database.CourseRegistrations
            .Include(item => item.Course)
            .Where(item =>
                item.StudentId == currentStudent.Id &&
                item.AcademicYear == Settings.AcademicYear &&
                item.Semester == Settings.Semester &&
                item.Status == "Confirmed")
            .AsNoTracking()
            .ToListAsync();
        var courseIds = registrations.Select(item => item.CourseId).ToList();
        var entries = await database.TimetableEntries
            .Include(item => item.Course)
            .Include(item => item.Room)
            .Include(item => item.Lecturer)
            .Where(item =>
                courseIds.Contains(item.CourseId) &&
                item.AcademicYear == Settings.AcademicYear &&
                item.Semester == Settings.Semester &&
                item.Status == "Published")
            .AsNoTracking()
            .ToListAsync();
        Entries = entries
            .OrderBy(item => DayOrder(item.DayOfWeek))
            .ThenBy(item => item.StartMinutes)
            .ToList();
        var scheduledIds = Entries.Select(item => item.CourseId).ToHashSet();
        RegistrationsWithoutTimetable = registrations
            .Where(item => !scheduledIds.Contains(item.CourseId))
            .ToList();
        return Page();
    }

    private static int DayOrder(string day) => day switch
    {
        "Monday" => 1,
        "Tuesday" => 2,
        "Wednesday" => 3,
        "Thursday" => 4,
        "Friday" => 5,
        _ => 99
    };
}
