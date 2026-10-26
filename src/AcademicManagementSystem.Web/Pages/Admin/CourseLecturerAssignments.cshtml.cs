using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class CourseLecturerAssignmentsModel(
    AcademicManagementDbContext database) : PageModel
{
    [BindProperty]
    public int CourseId { get; set; }

    [BindProperty]
    public int LecturerId { get; set; }

    [BindProperty]
    public bool IsPrimary { get; set; } = true;

    public IReadOnlyList<Course> Courses { get; private set; } = [];

    public IReadOnlyList<Lecturer> Lecturers { get; private set; } = [];

    public IReadOnlyList<CourseLecturerAssignment> Assignments { get; private set; } = [];

    public async Task OnGetAsync() => await LoadPageAsync();

    public async Task<IActionResult> OnPostAddAsync()
    {
        var course = await database.Courses
            .SingleOrDefaultAsync(item => item.Id == CourseId && item.IsActive);

        var lecturer = await database.Lecturers
            .SingleOrDefaultAsync(item => item.Id == LecturerId && item.IsActive);

        if (course is null || lecturer is null)
        {
            TempData["Error"] = "Select an active course and lecturer.";
            return RedirectToPage();
        }

        var alreadyAssigned = await database.CourseLecturerAssignments
            .AnyAsync(item =>
                item.CourseId == CourseId &&
                item.LecturerId == LecturerId);

        if (alreadyAssigned)
        {
            TempData["Error"] =
                $"{lecturer.FullName} is already assigned to {course.Code}.";
            return RedirectToPage();
        }

        if (IsPrimary)
        {
            var currentPrimaryAssignments = await database.CourseLecturerAssignments
                .Where(item => item.CourseId == CourseId && item.IsPrimary)
                .ToListAsync();

            foreach (var assignment in currentPrimaryAssignments)
            {
                assignment.IsPrimary = false;
            }
        }

        database.CourseLecturerAssignments.Add(new CourseLecturerAssignment
        {
            CourseId = course.Id,
            LecturerId = lecturer.Id,
            IsPrimary = IsPrimary,
            IsActive = true
        });

        await database.SaveChangesAsync();

        TempData["Success"] =
            $"{lecturer.FullName} was assigned to {course.Code}.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMakePrimaryAsync(int id)
    {
        var assignment = await database.CourseLecturerAssignments
            .SingleOrDefaultAsync(item => item.Id == id);

        if (assignment is null)
        {
            TempData["Error"] = "The assignment no longer exists.";
            return RedirectToPage();
        }

        var currentPrimaryAssignments = await database.CourseLecturerAssignments
            .Where(item =>
                item.CourseId == assignment.CourseId &&
                item.IsPrimary)
            .ToListAsync();

        foreach (var current in currentPrimaryAssignments)
        {
            current.IsPrimary = false;
        }

        assignment.IsPrimary = true;
        await database.SaveChangesAsync();

        TempData["Success"] = "Primary lecturer updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(int id)
    {
        var assignment = await database.CourseLecturerAssignments
            .Include(item => item.Course)
            .Include(item => item.Lecturer)
            .SingleOrDefaultAsync(item => item.Id == id);

        if (assignment is null)
        {
            TempData["Error"] = "The assignment no longer exists.";
            return RedirectToPage();
        }

        var description = $"{assignment.Lecturer.FullName} from {assignment.Course.Code}";

        database.CourseLecturerAssignments.Remove(assignment);
        await database.SaveChangesAsync();

        TempData["Success"] = $"Removed {description}.";
        return RedirectToPage();
    }

    private async Task LoadPageAsync()
    {
        Courses = await database.Courses
            .Where(course => course.IsActive)
            .OrderBy(course => course.Code)
            .ToListAsync();

        Lecturers = await database.Lecturers
            .Where(lecturer => lecturer.IsActive)
            .OrderBy(lecturer => lecturer.FullName)
            .ToListAsync();

        Assignments = await database.CourseLecturerAssignments
            .Include(assignment => assignment.Course)
            .Include(assignment => assignment.Lecturer)
            .OrderBy(assignment => assignment.Course.Code)
            .ThenByDescending(assignment => assignment.IsPrimary)
            .ThenBy(assignment => assignment.Lecturer.FullName)
            .ToListAsync();
    }
}