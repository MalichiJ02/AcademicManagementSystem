using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEntity = AcademicManagementSystem.Web.Models.Student;

namespace AcademicManagementSystem.Web.Pages.Coordinator;

[Authorize(Roles = "Coordinator,Administrator")]
public class AttendanceModel(
    AcademicManagementDbContext database,
    AcademicSettingsService settingsService) : PageModel
{
    private static readonly HashSet<string> ValidStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Present", "Late", "Absent" };

    [BindProperty(SupportsGet = true)]
    public int? SessionId { get; set; }

    [BindProperty]
    public int NewCourseId { get; set; }

    [BindProperty]
    public DateTime NewSessionDate { get; set; } = DateTime.Today;

    [BindProperty]
    public string NewSessionTopic { get; set; } = string.Empty;

    [BindProperty]
    public string StudentNumber { get; set; } = string.Empty;

    [BindProperty]
    public string AttendanceStatus { get; set; } = "Present";

    public IReadOnlyList<Course> Courses { get; private set; } = [];
    public IReadOnlyList<AttendanceSession> Sessions { get; private set; } = [];
    public AttendanceSession? SelectedSession { get; private set; }
    public IReadOnlyList<StudentEntity> EnrolledStudents { get; private set; } = [];
    public AcademicSettings Settings { get; private set; } = new();

    public async Task OnGetAsync() => await LoadPageAsync();

    public async Task<IActionResult> OnPostCreateSessionAsync()
    {
        Settings = await settingsService.GetAsync();
        var course = await database.Courses
            .SingleOrDefaultAsync(item => item.Id == NewCourseId && item.IsActive);
        if (course is null || string.IsNullOrWhiteSpace(NewSessionTopic))
        {
            TempData["Error"] = "Choose a course and enter a session topic.";
            return RedirectToPage();
        }
        if (!IsOfferedThisSemester(course, Settings.Semester))
        {
            TempData["Error"] = $"{course.Code} is not offered in {Settings.Semester}.";
            return RedirectToPage();
        }

        var session = new AttendanceSession
        {
            CourseId = course.Id,
            SessionDate = NewSessionDate.Date,
            Topic = NewSessionTopic.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        database.AttendanceSessions.Add(session);
        await database.SaveChangesAsync();
        TempData["Success"] = $"A new attendance session was created for {course.Code}.";
        return RedirectToPage(new { sessionId = session.Id });
    }

    public async Task<IActionResult> OnPostRecordAsync()
    {
        Settings = await settingsService.GetAsync();
        if (!SessionId.HasValue || !ValidStatuses.Contains(AttendanceStatus))
        {
            TempData["Error"] = "Choose a valid session and attendance status.";
            return RedirectToPage();
        }

        var session = await database.AttendanceSessions
            .Include(item => item.Course)
            .SingleOrDefaultAsync(item => item.Id == SessionId.Value);
        if (session is null)
        {
            TempData["Error"] = "The selected attendance session was not found.";
            return RedirectToPage();
        }

        var student = await database.Students
            .SingleOrDefaultAsync(item =>
                item.StudentNumber == StudentNumber.Trim() &&
                item.IsActive);
        if (student is null)
        {
            TempData["Error"] = "No active student was found for that student-ID number.";
            return RedirectToPage(new { sessionId = session.Id });
        }

        var isRegistered = await database.CourseRegistrations.AnyAsync(registration =>
            registration.StudentId == student.Id &&
            registration.CourseId == session.CourseId &&
            registration.AcademicYear == Settings.AcademicYear &&
            registration.Semester == Settings.Semester &&
            registration.Status == "Confirmed");
        if (!isRegistered)
        {
            TempData["Error"] =
                $"{student.StudentNumber} is not confirmed for {session.Course.Code} in this semester.";
            return RedirectToPage(new { sessionId = session.Id });
        }

        var record = await database.AttendanceRecords
            .SingleOrDefaultAsync(item =>
                item.AttendanceSessionId == session.Id &&
                item.StudentId == student.Id);
        if (record is null)
        {
            database.AttendanceRecords.Add(new AttendanceRecord
            {
                AttendanceSessionId = session.Id,
                StudentId = student.Id,
                Status = AttendanceStatus,
                RecordedAt = DateTime.UtcNow
            });
        }
        else
        {
            record.Status = AttendanceStatus;
            record.RecordedAt = DateTime.UtcNow;
        }

        await database.SaveChangesAsync();
        TempData["Success"] =
            $"{student.FullName} was marked {AttendanceStatus.ToLowerInvariant()} for {session.Course.Code}.";
        return RedirectToPage(new { sessionId = session.Id });
    }

    public AttendanceRecord? GetRecordFor(int studentId) =>
        SelectedSession?.Records.SingleOrDefault(record => record.StudentId == studentId);

    private async Task LoadPageAsync()
    {
        Settings = await settingsService.GetAsync();
        Courses = await database.Courses
            .Where(course =>
                course.IsActive &&
                (course.SemesterOffered == "Both" ||
                 course.SemesterOffered == Settings.Semester))
            .AsNoTracking()
            .OrderBy(course => course.Code)
            .ToListAsync();
        Sessions = await database.AttendanceSessions
            .Include(session => session.Course)
            .AsNoTracking()
            .OrderByDescending(session => session.SessionDate)
            .ThenBy(session => session.Course.Code)
            .ToListAsync();

        SessionId ??= Sessions.FirstOrDefault()?.Id;
        if (!SessionId.HasValue)
        {
            return;
        }

        SelectedSession = await database.AttendanceSessions
            .Include(session => session.Course)
            .Include(session => session.Records)
                .ThenInclude(record => record.Student)
            .AsSplitQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(session => session.Id == SessionId.Value);
        if (SelectedSession is null)
        {
            return;
        }

        EnrolledStudents = await database.CourseRegistrations
            .Where(registration =>
                registration.CourseId == SelectedSession.CourseId &&
                registration.AcademicYear == Settings.AcademicYear &&
                registration.Semester == Settings.Semester &&
                registration.Status == "Confirmed")
            .Select(registration => registration.Student)
            .AsNoTracking()
            .OrderBy(student => student.StudentNumber)
            .ToListAsync();
    }

    private static bool IsOfferedThisSemester(Course course, string semester) =>
        course.SemesterOffered.Equals("Both", StringComparison.OrdinalIgnoreCase) ||
        course.SemesterOffered.Equals(semester, StringComparison.OrdinalIgnoreCase);
}
