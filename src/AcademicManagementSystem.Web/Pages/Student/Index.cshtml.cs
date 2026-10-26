using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEntity = AcademicManagementSystem.Web.Models.Student;

namespace AcademicManagementSystem.Web.Pages.StudentPortal;

[Authorize(Roles = "Student")]
public class IndexModel(
    AcademicManagementDbContext database,
    CurrentStudentService currentStudentService,
    AcademicSettingsService settingsService,
    StudentAccessService accessService,
    AttendanceService attendanceService,
    AcademicRiskService riskService,
    AcademicProgressionService progressionService) : PageModel
{
    public StudentEntity? Student { get; private set; }
    public AcademicSettings Settings { get; private set; } = new();
    public Invoice? CurrentInvoice { get; private set; }
    public FinancialClearance? Clearance { get; private set; }
    public AccessDecision? RegistrationAccess { get; private set; }
    public AccessDecision? ExamSlipAccess { get; private set; }
    public AccessDecision? ResultsAccess { get; private set; }
    public IReadOnlyList<CourseRegistration> Registrations { get; private set; } = [];
    public AttendanceSummary? Attendance { get; private set; }
    public AcademicRiskAssessment? RiskAssessment { get; private set; }
    public ProgressionAssessment? Progression { get; private set; }

    public async Task OnGetAsync()
    {
        var currentStudent = await currentStudentService.GetStudentAsync(User);
        if (currentStudent is null || !currentStudent.IsActive)
        {
            return;
        }

        Settings = await settingsService.GetAsync();
        Student = await database.Students
            .Include(student => student.ProgrammeRecord)
            .Include(student => student.StudyModeRecord)
            .Include(student => student.Invoices)
            .Include(student => student.Registrations)
                .ThenInclude(registration => registration.Course)
            .Include(student => student.Results)
            .Include(student => student.CompletedCourses)
            .Include(student => student.AttendanceRecords)
                .ThenInclude(record => record.AttendanceSession)
                    .ThenInclude(session => session.Course)
            .AsSplitQuery()
            .AsNoTracking()
            .SingleAsync(student => student.Id == currentStudent.Id);

        CurrentInvoice = Student.Invoices
            .SingleOrDefault(invoice =>
                invoice.AcademicYear == Settings.AcademicYear &&
                invoice.Semester == Settings.Semester);
        Registrations = Student.Registrations
            .Where(registration =>
                registration.AcademicYear == Settings.AcademicYear &&
                registration.Semester == Settings.Semester &&
                registration.Status == "Confirmed")
            .OrderBy(registration => registration.Course.Code)
            .ToList();
        Progression = progressionService.Assess(Student, Settings);
        Attendance = attendanceService.Summarise(Student.AttendanceRecords);
        RiskAssessment = riskService.Assess(Student);

        if (CurrentInvoice is null)
        {
            RegistrationAccess = new AccessDecision(
                false,
                ["Generate your current invoice before registration can be assessed."]);
            ExamSlipAccess = new AccessDecision(
                false,
                ["Generate an invoice and complete a confirmed registration first."]);
            ResultsAccess = new AccessDecision(
                false,
                ["Generate an invoice before result access can be assessed."]);
            return;
        }

        Clearance = accessService.EvaluateFinancialClearance(CurrentInvoice, Settings);
        RegistrationAccess = accessService.EvaluateRegistration(Student, CurrentInvoice, Settings);
        ExamSlipAccess = accessService.EvaluateExamSlip(CurrentInvoice, Registrations.Count > 0, Settings);
        ResultsAccess = accessService.EvaluateResults(
            CurrentInvoice,
            Student.Results.Any(result => result.IsPublished),
            Settings);
    }
}
