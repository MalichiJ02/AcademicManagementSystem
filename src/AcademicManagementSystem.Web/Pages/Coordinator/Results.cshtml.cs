using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEntity = AcademicManagementSystem.Web.Models.Student;

namespace AcademicManagementSystem.Web.Pages.Coordinator;

[Authorize(Roles = "Coordinator,Administrator")]
public class ResultsModel(
    AcademicManagementDbContext database,
    AcademicSettingsService settingsService,
    AcademicProgressionService progressionService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public ResultInput NewResult { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ProgrammeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? YearOfStudy { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? BulkCourseId { get; set; }

    public IReadOnlyList<Course> BulkCourses { get; private set; } = [];
    public IReadOnlyList<BulkResultRow> BulkRows { get; private set; } = [];

    public IReadOnlyList<Programme> Programmes { get; private set; } = [];
    public IReadOnlyList<int> StudyYears { get; private set; } = [];

    public AcademicSettings Settings { get; private set; } = new();
    public IReadOnlyList<StudentEntity> Students { get; private set; } = [];
    public IReadOnlyList<Course> Courses { get; private set; } = [];
    public IReadOnlyList<ResultPublicationGroup> PendingGroups { get; private set; } = [];
    public IReadOnlyList<StudentResult> PublishedResults { get; private set; } = [];

    public async Task OnGetAsync(int studentId)
    {
        NewResult.StudentId = studentId;
        await LoadPageAsync();
    }

    private IQueryable<CourseRegistration> ConfirmedRegistrations(int studentId) =>
        database.CourseRegistrations.Where(registration =>
            registration.StudentId == studentId &&
            registration.AcademicYear == Settings.AcademicYear &&
            registration.Semester == Settings.Semester &&
            registration.Status == "Confirmed");

    private async Task<IQueryable<StudentEntity>> ManagedStudentsAsync()
    {
        if (User.IsInRole("Administrator"))
        {
            return database.Students;
        }

        var user = await userManager.GetUserAsync(User);
        return user?.CoordinatingSchoolId is int schoolId
            ? database.Students.Where(student =>
                student.ProgrammeRecord != null && student.ProgrammeRecord.SchoolId == schoolId)
            : database.Students.Where(student => false);
    }

    private IQueryable<CourseRegistration> BulkRegistrations(IQueryable<StudentEntity> students) =>
        database.CourseRegistrations.Where(registration =>
            ProgrammeId.HasValue &&
            YearOfStudy.HasValue &&
            registration.AcademicYear == Settings.AcademicYear &&
            registration.Semester == Settings.Semester &&
            registration.Status == "Confirmed" &&
            students.Any(student => student.Id == registration.StudentId &&
                student.ProgrammeId == ProgrammeId && student.YearOfStudy == YearOfStudy));

    public async Task<IActionResult> OnPostBulkRecordAsync(List<BulkMarkInput> marks)
    {
        Settings = await settingsService.GetAsync();
        if (!ModelState.IsValid || BulkCourseId is not int courseId ||
            marks.Any(row => row.Mark is < 0m or > 100m) ||
            marks.Select(row => row.StudentId).Distinct().Count() != marks.Count)
        {
            TempData["Error"] = "Check the course and enter marks between 0 and 100.";
            return RedirectToPage(new { ProgrammeId, YearOfStudy, BulkCourseId });
        }

        await using var transaction = await database.Database.BeginTransactionAsync();
        var registrations = BulkRegistrations(await ManagedStudentsAsync());
        var eligible = await registrations.Where(row => row.CourseId == courseId)
            .Select(row => row.StudentId).Distinct().ToListAsync();
        var course = await database.Courses.FindAsync(courseId);
        var entered = marks.Where(row => row.Mark.HasValue).ToList();
        if (course is null || entered.Count == 0 || entered.Any(row => !eligible.Contains(row.StudentId)))
        {
            TempData["Error"] = "Enter at least one mark for an eligible student in the selected course.";
            return RedirectToPage(new { ProgrammeId, YearOfStudy, BulkCourseId });
        }

        var existing = await database.StudentResults
            .Where(row => row.CourseId == courseId && eligible.Contains(row.StudentId) &&
                row.AcademicYear == Settings.AcademicYear && row.Semester == Settings.Semester)
            .ToDictionaryAsync(row => row.StudentId);
        if (entered.Any(row => existing.TryGetValue(row.StudentId, out var result) && result.IsPublished))
        {
            TempData["Error"] = "A submitted mark is already published. No marks were changed.";
            return RedirectToPage(new { ProgrammeId, YearOfStudy, BulkCourseId });
        }

        foreach (var row in entered)
        {
            if (!existing.TryGetValue(row.StudentId, out var result))
            {
                result = new StudentResult
                {
                    StudentId = row.StudentId,
                    CourseId = courseId,
                    CourseCode = course.Code,
                    AcademicYear = Settings.AcademicYear,
                    Semester = Settings.Semester
                };
                database.StudentResults.Add(result);
            }

            result.Mark = row.Mark!.Value;
            result.Grade = GradeFor(result.Mark);
        }

        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = $"Saved {entered.Count} draft mark(s) for {course.Code}.";
        return RedirectToPage(new { ProgrammeId, YearOfStudy, BulkCourseId });
    }

    public async Task<IActionResult> OnPostRecordAsync()
    {
        Settings = await settingsService.GetAsync();
        if (!ModelState.IsValid || NewResult.StudentId <= 0 || NewResult.CourseId <= 0 ||
            NewResult.Mark is < 0m or > 100m)
        {
            ModelState.AddModelError(string.Empty, "Choose a student and confirmed course, then enter a mark from 0 to 100.");
            await LoadPageAsync();
            return Page();
        }

        await using var transaction = await database.Database.BeginTransactionAsync();
        var student = await (await ManagedStudentsAsync())
            .SingleOrDefaultAsync(item => item.Id == NewResult.StudentId);
        var course = await database.Courses.FindAsync(NewResult.CourseId);
        var isRegistered = await ConfirmedRegistrations(NewResult.StudentId)
            .AnyAsync(registration => registration.CourseId == NewResult.CourseId);
        if (student is null || course is null || !isRegistered)
        {
            TempData["Error"] = "Choose a student you manage with a confirmed registration in that course.";
            return RedirectToPage(new { studentId = NewResult.StudentId, Search, ProgrammeId, YearOfStudy });
        }

        var result = await database.StudentResults.SingleOrDefaultAsync(item =>
            item.StudentId == student.Id &&
            item.CourseId == course.Id &&
            item.AcademicYear == Settings.AcademicYear &&
            item.Semester == Settings.Semester);
        if (result is null)
        {
            result = new StudentResult
            {
                StudentId = student.Id,
                CourseId = course.Id,
                CourseCode = course.Code,
                AcademicYear = Settings.AcademicYear,
                Semester = Settings.Semester,
                IsPublished = false
            };
            database.StudentResults.Add(result);
        }

        result.Mark = NewResult.Mark;
        result.Grade = GradeFor(NewResult.Mark);
        result.IsPublished = false;
        result.PublishedAt = null;
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = $"Result for {student.FullName} in {course.Code} was saved as unpublished.";
        return RedirectToPage(new { studentId = student.Id, Search, ProgrammeId, YearOfStudy });
    }

    public async Task<IActionResult> OnPostPublishAsync(int studentId)
    {
        Settings = await settingsService.GetAsync();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var managedStudents = await ManagedStudentsAsync();
        if (!await managedStudents.AnyAsync(student => student.Id == studentId))
        {
            return Forbid();
        }

        var results = await database.StudentResults
            .Include(result => result.Student)
            .Include(result => result.Course)
            .Where(result =>
                result.StudentId == studentId &&
                result.AcademicYear == Settings.AcademicYear &&
                result.Semester == Settings.Semester &&
                !result.IsPublished)
            .ToListAsync();
        if (results.Count == 0)
        {
            TempData["Error"] = "No unpublished result batch was found for that student.";
            return RedirectToPage();
        }

        var student = await database.Students
            .Include(item => item.Results)
            .Include(item => item.CompletedCourses)
            .SingleAsync(item => item.Id == studentId);
        foreach (var result in results)
        {
            result.IsPublished = true;
            result.PublishedAt = DateTime.UtcNow;
            var completedCourse = await database.CompletedCourses
                .SingleOrDefaultAsync(item =>
                    item.StudentId == student.Id &&
                    item.CourseCode == result.CourseCode);
            if (completedCourse is null)
            {
                completedCourse = new CompletedCourse
                {
                    StudentId = student.Id,
                    CourseId = result.CourseId,
                    CourseCode = result.CourseCode
                };
                database.CompletedCourses.Add(completedCourse);
            }

            completedCourse.CourseId = result.CourseId;
            completedCourse.IsPassed = result.Mark >= 50m;
            completedCourse.Mark = result.Mark;
            completedCourse.Grade = result.Grade;
            completedCourse.AcademicYear = Settings.AcademicYear;
            completedCourse.Semester = Settings.Semester;
        }

        progressionService.Apply(student, Settings);
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] =
            $"{results.Count} result(s) were published for {student.FullName}. Progression status was recalculated.";
        return RedirectToPage();
    }

    private async Task LoadPageAsync()
    {
        Settings = await settingsService.GetAsync();
        var managedStudents = await ManagedStudentsAsync();
        var students = managedStudents.AsNoTracking();
        var registrations = BulkRegistrations(managedStudents);
        BulkCourses = await database.Courses.AsNoTracking()
            .Where(course => registrations.Any(row => row.CourseId == course.Id))
            .OrderBy(course => course.Code).ToListAsync();

        if (BulkCourseId is int bulkCourseId && BulkCourses.Any(course => course.Id == bulkCourseId))
        {
            var eligibleIds = registrations.Where(row => row.CourseId == bulkCourseId)
                .Select(row => row.StudentId);
            var results = await database.StudentResults.AsNoTracking()
                .Where(row => row.CourseId == bulkCourseId && eligibleIds.Contains(row.StudentId) &&
                    row.AcademicYear == Settings.AcademicYear && row.Semester == Settings.Semester)
                .ToDictionaryAsync(row => row.StudentId);
            var roster = await students.Where(student => eligibleIds.Contains(student.Id))
                .OrderBy(student => student.StudentNumber).ToListAsync();
            BulkRows = roster.Select(student => new BulkResultRow(student,
                results.GetValueOrDefault(student.Id))).ToList();
        }

        var programmeIds = managedStudents.Select(student => student.ProgrammeId);
        Programmes = await database.Programmes.AsNoTracking()
            .Where(programme => programmeIds.Contains(programme.Id))
            .OrderBy(programme => programme.Code)
            .ToListAsync();
        StudyYears = await students.Select(student => student.YearOfStudy)
            .Distinct().OrderBy(year => year).ToListAsync();

        if (ProgrammeId is int programmeId)
        {
            students = students.Where(student => student.ProgrammeId == programmeId);
        }

        if (YearOfStudy is int yearOfStudy)
        {
            students = students.Where(student => student.YearOfStudy == yearOfStudy);
        }

        var query = Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(query))
        {
            students = students.Where(student =>
                student.StudentNumber.ToLower().Contains(query) ||
                (student.FirstName + " " + student.LastName).ToLower().Contains(query));
        }

        Students = await students.OrderBy(student => student.StudentNumber).ToListAsync();
        if (!Students.Any(student => student.Id == NewResult.StudentId))
        {
            NewResult.StudentId = 0;
        }

        var registeredCourseIds = ConfirmedRegistrations(NewResult.StudentId)
            .Select(registration => registration.CourseId);
        Courses = NewResult.StudentId <= 0
            ? []
            : await database.Courses
                .Where(course => registeredCourseIds.Contains(course.Id))
                .AsNoTracking()
                .OrderBy(course => course.Code)
                .ToListAsync();
        var allResults = await database.StudentResults
            .Include(result => result.Student)
            .Include(result => result.Course)
            .Where(result =>
                managedStudents.Any(student => student.Id == result.StudentId) &&
                result.AcademicYear == Settings.AcademicYear &&
                result.Semester == Settings.Semester)
            .AsNoTracking()
            .OrderBy(result => result.Student.StudentNumber)
            .ThenBy(result => result.CourseCode)
            .ToListAsync();
        PendingGroups = allResults
            .Where(result => !result.IsPublished)
            .GroupBy(result => result.StudentId)
            .Select(group => new ResultPublicationGroup(
                group.Key,
                group.First().Student.FullName,
                group.First().Student.StudentNumber,
                group.ToList()))
            .ToList();
        PublishedResults = allResults.Where(result => result.IsPublished).ToList();
    }

    private static string GradeFor(decimal mark) => mark switch
    {
        >= 85m => "A+",
        >= 75m => "A",
        >= 70m => "B+",
        >= 65m => "B",
        >= 60m => "B-",
        >= 55m => "C+",
        >= 50m => "C",
        >= 45m => "D",
        _ => "F"
    };
}

public sealed record ResultPublicationGroup(
    int StudentId,
    string StudentName,
    string StudentNumber,
    IReadOnlyList<StudentResult> Results);

public sealed class ResultInput
{
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public decimal Mark { get; set; }
}


public sealed record BulkResultRow(StudentEntity Student, StudentResult? Result);

public sealed class BulkMarkInput
{
    public int StudentId { get; set; }
    public decimal? Mark { get; set; }
}
