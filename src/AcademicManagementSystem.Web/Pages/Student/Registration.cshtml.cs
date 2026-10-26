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
public class RegistrationModel(
    AcademicManagementDbContext database,
    CurrentStudentService currentStudentService,
    AcademicSettingsService settingsService,
    StudentAccessService accessService,
    RegistrationService registrationService,
    AcademicProgressionService progressionService) : PageModel
{
    [BindProperty]
    public List<int> SelectedCourseIds { get; set; } = [];

    public StudentEntity? Student { get; private set; }
    public AcademicSettings Settings { get; private set; } = new();
    public Invoice? CurrentInvoice { get; private set; }
    public AccessDecision? RegistrationAccess { get; private set; }
    public ProgressionAssessment? Progression { get; private set; }
    public IReadOnlyList<Course> AvailableCourses { get; private set; } = [];
    public IReadOnlyList<CourseRegistration> ExistingRegistrations { get; private set; } = [];
    public IReadOnlyList<TimetableEntry> TimetableEntries { get; private set; } = [];
    public HashSet<int> RegisteredCourseIds { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadPageAsync())
        {
            return RedirectToPage("/Account/AccessDenied");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var currentStudent = await currentStudentService.GetStudentAsync(User);
        if (currentStudent is null || !currentStudent.IsActive)
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        var settings = await settingsService.GetAsync();
        var student = await database.Students
            .Include(item => item.Invoices)
            .Include(item => item.CompletedCourses)
            .Include(item => item.Results)
            .Include(item => item.StudyModeRecord)
            .SingleAsync(item => item.Id == currentStudent.Id);

        var invoice = student.Invoices.SingleOrDefault(item =>
            item.AcademicYear == settings.AcademicYear &&
            item.Semester == settings.Semester);
        if (invoice is null)
        {
            TempData["Error"] = "Generate your current invoice before selecting courses.";
            return RedirectToPage();
        }

        var access = accessService.EvaluateRegistration(student, invoice, settings);
        if (!access.Allowed)
        {
            TempData["Error"] = string.Join(" ", access.Reasons);
            return RedirectToPage();
        }

        var selectedIds = SelectedCourseIds.Distinct().ToList();
        if (selectedIds.Count == 0)
        {
            TempData["Error"] = "Select at least one course before submitting registration.";
            return RedirectToPage();
        }

        var selectedCourses = await database.Courses
            .Include(course => course.Prerequisites)
                .ThenInclude(link => link.PrerequisiteCourse)
            .Where(course => selectedIds.Contains(course.Id) && course.IsActive)
            .ToListAsync();
        if (selectedCourses.Count != selectedIds.Count)
        {
            TempData["Error"] = "One or more selected courses are no longer active.";
            return RedirectToPage();
        }

        var existingRegistrations = await database.CourseRegistrations
            .Include(registration => registration.Course)
            .Where(registration =>
                registration.StudentId == student.Id &&
                registration.AcademicYear == settings.AcademicYear &&
                registration.Semester == settings.Semester &&
                registration.Status == "Confirmed")
            .ToListAsync();
        var courseIdsToValidate = selectedCourses.Select(course => course.Id)
            .Concat(existingRegistrations.Select(registration => registration.CourseId))
            .Distinct()
            .ToList();
        var timetableEntries = await database.TimetableEntries
            .Include(entry => entry.Room)
            .Where(entry =>
                entry.AcademicYear == settings.AcademicYear &&
                entry.Semester == settings.Semester &&
                courseIdsToValidate.Contains(entry.CourseId) &&
                entry.Status == "Published")
            .ToListAsync();

        var validation = registrationService.Validate(
            student,
            settings,
            selectedCourses,
            existingRegistrations,
            timetableEntries);
        if (!validation.IsValid)
        {
            TempData["Error"] = string.Join(" ", validation.Errors);
            return RedirectToPage();
        }

        progressionService.Apply(student, settings);
        foreach (var course in selectedCourses)
        {
            database.CourseRegistrations.Add(new CourseRegistration
            {
                StudentId = student.Id,
                CourseId = course.Id,
                AcademicYear = settings.AcademicYear,
                Semester = settings.Semester,
                RegisteredAt = DateTime.UtcNow,
                Status = "Confirmed",
                RegistrationType = validation.RegistrationTypes[course.Id]
            });
        }

        await database.SaveChangesAsync();
        TempData["Success"] =
            $"{selectedCourses.Count} course(s) registered successfully. " +
            $"You now have {validation.TotalCourseCount} confirmed course(s), " +
            $"including {validation.CarryCourseCount} carry course(s).";
        return RedirectToPage();
    }

    public string GetCourseSchedule(Course course)
    {
        var entry = TimetableEntries
            .Where(item => item.CourseId == course.Id)
            .OrderBy(item => item.DayOfWeek)
            .ThenBy(item => item.StartMinutes)
            .FirstOrDefault();
        return entry is null ? "Awaiting timetable allocation" : $"{entry.ScheduleLabel} · {entry.Room.Code}";
    }

    private async Task<bool> LoadPageAsync()
    {
        var currentStudent = await currentStudentService.GetStudentAsync(User);
        if (currentStudent is null || !currentStudent.IsActive)
        {
            return false;
        }

        Settings = await settingsService.GetAsync();
        Student = await database.Students
            .Include(student => student.ProgrammeRecord)
            .Include(student => student.StudyModeRecord)
            .Include(student => student.Invoices)
            .Include(student => student.CompletedCourses)
            .Include(student => student.Results)
            .Include(student => student.Registrations)
                .ThenInclude(registration => registration.Course)
            .AsSplitQuery()
            .AsNoTracking()
            .SingleAsync(student => student.Id == currentStudent.Id);
        CurrentInvoice = Student.Invoices.SingleOrDefault(invoice =>
            invoice.AcademicYear == Settings.AcademicYear &&
            invoice.Semester == Settings.Semester);
        ExistingRegistrations = Student.Registrations
            .Where(registration =>
                registration.AcademicYear == Settings.AcademicYear &&
                registration.Semester == Settings.Semester &&
                registration.Status == "Confirmed")
            .OrderBy(registration => registration.Course.Code)
            .ToList();
        RegisteredCourseIds = ExistingRegistrations.Select(item => item.CourseId).ToHashSet();
        Progression = progressionService.Assess(Student, Settings);

        if (CurrentInvoice is not null)
        {
            RegistrationAccess = accessService.EvaluateRegistration(Student, CurrentInvoice, Settings);
        }
        else
        {
            RegistrationAccess = new AccessDecision(false, ["Generate your current invoice first."]);
        }

        AvailableCourses = await database.Courses
            .Include(course => course.Prerequisites)
                .ThenInclude(link => link.PrerequisiteCourse)
            .AsSplitQuery()
            .AsNoTracking()
            .Where(course =>
                course.IsActive &&
                (!course.ProgrammeId.HasValue || course.ProgrammeId == Student.ProgrammeId) &&
                (!course.StudyModeId.HasValue || course.StudyModeId == Student.StudyModeId))
            .OrderBy(course => course.YearLevel)
            .ThenBy(course => course.Code)
            .ToListAsync();
        TimetableEntries = await database.TimetableEntries
            .Include(entry => entry.Room)
            .Where(entry =>
                entry.AcademicYear == Settings.AcademicYear &&
                entry.Semester == Settings.Semester &&
                entry.Status == "Published")
            .AsNoTracking()
            .ToListAsync();
        return true;
    }
}
