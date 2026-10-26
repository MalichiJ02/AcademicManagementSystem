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
public class ExamSlipModel(
    AcademicManagementDbContext database,
    CurrentStudentService currentStudentService,
    AcademicSettingsService settingsService,
    StudentAccessService accessService) : PageModel
{
    public StudentEntity? Student { get; private set; }
    public AcademicSettings Settings { get; private set; } = new();
    public Invoice? CurrentInvoice { get; private set; }
    public AccessDecision Access { get; private set; } = new(false, []);
    public IReadOnlyList<CourseRegistration> Registrations { get; private set; } = [];

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
            .Include(item => item.Registrations)
                .ThenInclude(item => item.Course)
            .AsSplitQuery()
            .AsNoTracking()
            .SingleAsync(item => item.Id == currentStudent.Id);
        CurrentInvoice = Student.Invoices.SingleOrDefault(item =>
            item.AcademicYear == Settings.AcademicYear &&
            item.Semester == Settings.Semester);
        Registrations = Student.Registrations
            .Where(item =>
                item.AcademicYear == Settings.AcademicYear &&
                item.Semester == Settings.Semester &&
                item.Status == "Confirmed")
            .OrderBy(item => item.Course.Code)
            .ToList();
        Access = CurrentInvoice is null
            ? new AccessDecision(false, ["Generate your current invoice before exam-slip eligibility can be assessed."])
            : accessService.EvaluateExamSlip(CurrentInvoice, Registrations.Count > 0, Settings);
        return Page();
    }
}
