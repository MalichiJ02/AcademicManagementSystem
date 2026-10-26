using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.StudentPortal;

[Authorize(Roles = "Student")]
public class AttendanceModel(
    AcademicManagementDbContext database,
    CurrentStudentService currentStudentService,
    AttendanceService attendanceService) : PageModel
{
    public AttendanceSummary? TotalAttendance { get; private set; }
    public IReadOnlyList<AttendanceCourseSummary> CourseSummaries { get; private set; } = [];
    public IReadOnlyList<AttendanceRecord> Records { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var currentStudent = await currentStudentService.GetStudentAsync(User);
        if (currentStudent is null || !currentStudent.IsActive)
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        var student = await database.Students
            .Include(item => item.AttendanceRecords)
                .ThenInclude(item => item.AttendanceSession)
                    .ThenInclude(item => item.Course)
            .AsSplitQuery()
            .AsNoTracking()
            .SingleAsync(item => item.Id == currentStudent.Id);

        Records = student.AttendanceRecords
            .OrderByDescending(item => item.AttendanceSession.SessionDate)
            .ToList();
        TotalAttendance = attendanceService.Summarise(Records);
        CourseSummaries = Records
            .GroupBy(record => record.AttendanceSession.CourseId)
            .Select(group =>
            {
                var course = group.First().AttendanceSession.Course;
                return new AttendanceCourseSummary(
                    course.Code,
                    course.Title,
                    attendanceService.Summarise(group));
            })
            .OrderBy(item => item.CourseCode)
            .ToList();
        return Page();
    }
}

public sealed record AttendanceCourseSummary(
    string CourseCode,
    string CourseTitle,
    AttendanceSummary Attendance);
