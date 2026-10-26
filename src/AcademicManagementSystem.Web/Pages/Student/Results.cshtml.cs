using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEntity = AcademicManagementSystem.Web.Models.Student;

namespace AcademicManagementSystem.Web.Pages.StudentPortal;

[Authorize(Roles = "Student")]
public class ResultsModel(
    AcademicManagementDbContext database,
    CurrentStudentService currentStudentService,
    AcademicSettingsService settingsService,
    StudentAccessService accessService) : PageModel
{
    public StudentEntity? Student { get; private set; }
    public AcademicSettings Settings { get; private set; } = new();
    public IReadOnlyList<ResultPeriod> ResultPeriods { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var currentStudent = await currentStudentService.GetStudentAsync(User);
        if (currentStudent is null || !currentStudent.IsActive)
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        Settings = await settingsService.GetAsync();
        Student = await database.Students
            .Include(item => item.Invoices)
            .Include(item => item.Results)
                .ThenInclude(item => item.Course)
            .AsSplitQuery()
            .AsNoTracking()
            .SingleAsync(item => item.Id == currentStudent.Id);

        ResultPeriods = Student.Results
            .Where(item => item.IsPublished)
            .GroupBy(item => new { item.AcademicYear, item.Semester })
            .OrderByDescending(group => group.Key.AcademicYear)
            .ThenByDescending(group => group.Key.Semester)
            .Select(group =>
            {
                var invoice = Student.Invoices.SingleOrDefault(item =>
                    item.AcademicYear == group.Key.AcademicYear &&
                    item.Semester == group.Key.Semester);
                var access = invoice is null
                    ? new AccessDecision(false, ["No matching invoice record is available for this result period."])
                    : accessService.EvaluateResults(invoice, group.Any(), Settings);
                return new ResultPeriod(
                    group.Key.AcademicYear,
                    group.Key.Semester,
                    invoice,
                    access,
                    group.OrderBy(item => item.CourseCode).ToList());
            })
            .ToList();
        return Page();
    }
}

public sealed record ResultPeriod(
    string AcademicYear,
    string Semester,
    Invoice? Invoice,
    AccessDecision Access,
    IReadOnlyList<StudentResult> Results);
