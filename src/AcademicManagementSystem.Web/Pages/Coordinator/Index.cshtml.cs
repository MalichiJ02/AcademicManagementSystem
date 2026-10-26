using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Coordinator;

[Authorize(Roles = "Coordinator,Administrator")]
public class IndexModel(
    AcademicManagementDbContext database,
    AcademicRiskService riskService,
    TimetableService timetableService,
    AcademicSettingsService settingsService) : PageModel
{
    public AcademicSettings Settings { get; private set; } = new();
    public int StudentCount { get; private set; }
    public int ConfirmedRegistrationCount { get; private set; }
    public int TimetableEntryCount { get; private set; }
    public int PendingTimetableCount { get; private set; }
    public int PublishedResultCount { get; private set; }
    public decimal? AverageAttendance { get; private set; }
    public IReadOnlyList<AcademicRiskAssessment> AtRiskStudents { get; private set; } = [];
    public IReadOnlyList<TimetableIssue> TimetableIssues { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Settings = await settingsService.GetAsync();
        var students = await database.Students
            .Include(student => student.Results)
            .Include(student => student.AttendanceRecords)
                .ThenInclude(record => record.AttendanceSession)
                    .ThenInclude(session => session.Course)
            .AsSplitQuery()
            .AsNoTracking()
            .OrderBy(student => student.StudentNumber)
            .ToListAsync();

        var timetableEntries = await database.TimetableEntries
            .Include(entry => entry.Course)
            .Include(entry => entry.Lecturer)
            .Include(entry => entry.Room)
            .Where(entry =>
                entry.AcademicYear == Settings.AcademicYear &&
                entry.Semester == Settings.Semester)
            .AsNoTracking()
            .ToListAsync();
        var registrations = await database.CourseRegistrations
            .Where(registration =>
                registration.AcademicYear == Settings.AcademicYear &&
                registration.Semester == Settings.Semester &&
                registration.Status == "Confirmed")
            .AsNoTracking()
            .ToListAsync();
        var activeCourses = await database.Courses
            .Where(course => course.IsActive)
            .AsNoTracking()
            .ToListAsync();

        var assessments = students
            .Select(riskService.Assess)
            .ToList();

        StudentCount = students.Count;
        ConfirmedRegistrationCount = registrations.Count;
        TimetableEntryCount = timetableEntries.Count;
        PendingTimetableCount = activeCourses.Count(course =>
            timetableEntries.All(entry => entry.CourseId != course.Id));
        PublishedResultCount = students.Sum(student => student.Results.Count(result => result.IsPublished));
        AverageAttendance = assessments
            .Where(assessment => assessment.AttendancePercentage.HasValue)
            .Select(assessment => assessment.AttendancePercentage!.Value)
            .DefaultIfEmpty()
            .Average();
        AtRiskStudents = assessments
            .Where(assessment => assessment.Level is "High" or "Moderate")
            .OrderByDescending(assessment => assessment.Level == "High")
            .ThenBy(assessment => assessment.StudentNumber)
            .ToList();
        TimetableIssues = timetableService.FindIssues(timetableEntries, registrations);
    }
}
