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
public class StudentsModel(
    AcademicManagementDbContext database,
    AcademicRiskService riskService,
    AcademicSettingsService settingsService) : PageModel
{
    [BindProperty]
    public int StudentId { get; set; }

    [BindProperty]
    public string EligibilityDecision { get; set; } = "Eligible";

    [BindProperty]
    public string? Reason { get; set; }

    public IReadOnlyList<StudentEntity> StudentOptions { get; private set; } = [];
    public IReadOnlyList<StudentOversightRow> Students { get; private set; } = [];
    public AcademicSettings Settings { get; private set; } = new();

    public async Task OnGetAsync() => await LoadPageAsync();

    public async Task<IActionResult> OnPostUpdateEligibilityAsync()
    {
        var student = await database.Students.SingleOrDefaultAsync(item => item.Id == StudentId);
        if (student is null)
        {
            TempData["Error"] = "Select a valid student for the coordinator decision.";
            return RedirectToPage();
        }

        var eligible = EligibilityDecision.Equals("Eligible", StringComparison.OrdinalIgnoreCase);
        student.IsAcademicallyEligible = eligible;
        student.AcademicHoldReason = eligible
            ? null
            : string.IsNullOrWhiteSpace(Reason)
                ? "Academic hold applied by the coordinator."
                : Reason.Trim();

        await database.SaveChangesAsync();
        TempData["Success"] = eligible
            ? $"{student.FullName} is now marked academically eligible."
            : $"An academic hold was recorded for {student.FullName}.";
        return RedirectToPage();
    }

    private async Task LoadPageAsync()
    {
        Settings = await settingsService.GetAsync();
        var students = await database.Students
            .Include(student => student.Invoices)
            .Include(student => student.Registrations)
                .ThenInclude(registration => registration.Course)
            .Include(student => student.Results)
            .Include(student => student.AttendanceRecords)
                .ThenInclude(record => record.AttendanceSession)
            .AsSplitQuery()
            .AsNoTracking()
            .OrderBy(student => student.StudentNumber)
            .ToListAsync();

        StudentOptions = students;
        Students = students.Select(student =>
        {
            var invoice = student.Invoices
                .OrderByDescending(item => item.LastSyncedAt)
                .FirstOrDefault();
            var assessment = riskService.Assess(student);
            return new StudentOversightRow(
                student.Id,
                student.StudentNumber,
                student.FullName,
                student.Programme,
                student.IsAcademicallyEligible,
                student.AcademicHoldReason,
                invoice?.PaymentPercentage,
                student.Registrations.Count(registration =>
                    registration.AcademicYear == Settings.AcademicYear &&
                    registration.Semester == Settings.Semester &&
                    registration.Status == "Confirmed"),
                assessment.Level);
        }).ToList();
    }
}

public sealed record StudentOversightRow(
    int StudentId,
    string StudentNumber,
    string StudentName,
    string Programme,
    bool IsAcademicallyEligible,
    string? AcademicHoldReason,
    decimal? PaymentPercentage,
    int RegistrationCount,
    string RiskLevel);
