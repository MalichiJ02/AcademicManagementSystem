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
public class ResultsModel(
    AcademicManagementDbContext database,
    AcademicSettingsService settingsService,
    AcademicProgressionService progressionService) : PageModel
{
    [BindProperty]
    public ResultInput NewResult { get; set; } = new();

    public AcademicSettings Settings { get; private set; } = new();
    public IReadOnlyList<StudentEntity> Students { get; private set; } = [];
    public IReadOnlyList<Course> Courses { get; private set; } = [];
    public IReadOnlyList<ResultPublicationGroup> PendingGroups { get; private set; } = [];
    public IReadOnlyList<StudentResult> PublishedResults { get; private set; } = [];

    public async Task OnGetAsync() => await LoadPageAsync();

    public async Task<IActionResult> OnPostRecordAsync()
    {
        Settings = await settingsService.GetAsync();
        if (NewResult.StudentId <= 0 || NewResult.CourseId <= 0 ||
            NewResult.Mark is < 0m or > 100m)
        {
            ModelState.AddModelError(string.Empty, "Choose a student and confirmed course, then enter a mark from 0 to 100.");
            await LoadPageAsync();
            return Page();
        }

        var student = await database.Students.FindAsync(NewResult.StudentId);
        var course = await database.Courses.FindAsync(NewResult.CourseId);
        var isRegistered = await database.CourseRegistrations.AnyAsync(registration =>
            registration.StudentId == NewResult.StudentId &&
            registration.CourseId == NewResult.CourseId &&
            registration.AcademicYear == Settings.AcademicYear &&
            registration.Semester == Settings.Semester &&
            registration.Status == "Confirmed");
        if (student is null || course is null || !isRegistered)
        {
            ModelState.AddModelError(
                string.Empty,
                "A result can only be entered for a student with a confirmed registration in that course.");
            await LoadPageAsync();
            return Page();
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
        TempData["Success"] = $"Result for {student.FullName} in {course.Code} was saved as unpublished.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPublishAsync(int studentId)
    {
        Settings = await settingsService.GetAsync();
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
        TempData["Success"] =
            $"{results.Count} result(s) were published for {student.FullName}. Progression status was recalculated.";
        return RedirectToPage();
    }

    private async Task LoadPageAsync()
    {
        Settings = await settingsService.GetAsync();
        Students = await database.Students
            .AsNoTracking()
            .OrderBy(item => item.StudentNumber)
            .ToListAsync();
        Courses = await database.Courses
            .Where(item => item.IsActive)
            .AsNoTracking()
            .OrderBy(item => item.Code)
            .ToListAsync();
        var allResults = await database.StudentResults
            .Include(result => result.Student)
            .Include(result => result.Course)
            .Where(result =>
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
