using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Coordinator;

[Authorize(Roles = "Coordinator,Administrator")]
public class TimetableModel(
    AcademicManagementDbContext database,
    TimetableService timetableService,
    AcademicSettingsService settingsService) : PageModel
{
    [BindProperty]
    public int CourseId { get; set; }

    [BindProperty]
    public int LecturerId { get; set; }

    [BindProperty]
    public int RoomId { get; set; }

    [BindProperty]
    public string SlotValue { get; set; } = string.Empty;

    [BindProperty]
    public int EntryId { get; set; }

    public IReadOnlyList<Course> Courses { get; private set; } = [];

    public IReadOnlyList<Course> AwaitingCourses { get; private set; } = [];

    public IReadOnlyList<Lecturer> Lecturers { get; private set; } = [];

    public IReadOnlyList<Room> Rooms { get; private set; } = [];

    public IReadOnlyList<TimetableEntry> Entries { get; private set; } = [];

    public IReadOnlyList<TimetableIssue> Issues { get; private set; } = [];

    public IReadOnlyList<CourseLecturerAssignment> CourseLecturerAssignments
    {
        get;
        private set;
    } = [];

    public AcademicSettings Settings { get; private set; } = new();

    public IReadOnlyList<TimetableSlot> Slots => TimetableService.StandardSlots;

    public async Task OnGetAsync() => await LoadPageAsync();

    public async Task<IActionResult> OnPostAddAsync()
    {
        var settings = await settingsService.GetAsync();
        Settings = settings;

        if (!TryGetSlot(SlotValue, out var slot))
        {
            TempData["Error"] = "Choose one of the available timetable periods.";
            return RedirectToPage();
        }

        var course = await database.Courses
            .Include(item => item.StudyMode)
            .SingleOrDefaultAsync(item => item.Id == CourseId && item.IsActive);

        var lecturer = await database.Lecturers
            .SingleOrDefaultAsync(item => item.Id == LecturerId && item.IsActive);

        var room = await database.Rooms
            .SingleOrDefaultAsync(item => item.Id == RoomId && item.IsActive);

        if (course is null || lecturer is null || room is null)
        {
            TempData["Error"] = "Select a valid active course, lecturer, and room.";
            return RedirectToPage();
        }

        var lecturerIsAssigned = await database.CourseLecturerAssignments
            .AnyAsync(assignment =>
                assignment.CourseId == course.Id &&
                assignment.LecturerId == lecturer.Id &&
                assignment.IsActive);

        if (!lecturerIsAssigned)
        {
            TempData["Error"] =
                $"{lecturer.FullName} is not assigned to teach {course.Code}. " +
                "Create the course–lecturer assignment first.";

            return RedirectToPage();
        }

        if (!IsOfferedThisSemester(course, settings.Semester))
        {
            TempData["Error"] = $"{course.Code} is not offered in {settings.Semester}.";
            return RedirectToPage();
        }

        if (!TimetableService.IsSlotCompatibleWithCourse(course, slot))
        {
            TempData["Error"] =
                course.StudyMode?.Name.Equals(
                    "Evening",
                    StringComparison.OrdinalIgnoreCase) == true
                    ? $"{course.Code} is an Evening course and must use an evening teaching period."
                    : $"{course.Code} must use a daytime teaching period.";

            return RedirectToPage();
        }

        var entries = await GetEntriesAsync();

        if (entries.Any(entry => entry.CourseId == course.Id))
        {
            TempData["Error"] = $"{course.Code} already has a timetable allocation.";
            return RedirectToPage();
        }

        var registrations = await GetCurrentRegistrationsAsync();

        var candidate = new TimetableEntry
        {
            CourseId = course.Id,
            Course = course,
            LecturerId = lecturer.Id,
            Lecturer = lecturer,
            RoomId = room.Id,
            Room = room,
            AcademicYear = settings.AcademicYear,
            Semester = settings.Semester,
            DayOfWeek = slot.DayOfWeek,
            StartMinutes = slot.StartMinutes,
            EndMinutes = slot.EndMinutes,
            Status = "Draft"
        };

        var validation = timetableService.Validate(candidate, entries, registrations);

        if (!validation.IsValid)
        {
            TempData["Error"] = string.Join(" ", validation.Errors);
            return RedirectToPage();
        }

        database.TimetableEntries.Add(candidate);

        course.ScheduleKey = candidate.ScheduleLabel;
        course.Venue = room.Code;

        await database.SaveChangesAsync();

        TempData["Success"] =
            $"{course.Code} was added as a draft allocation for {candidate.ScheduleLabel}.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostGenerateAsync()
    {
        var settings = await settingsService.GetAsync();
        Settings = settings;

        var entries = await GetEntriesAsync();
        var registrations = await GetCurrentRegistrationsAsync();

        var courses = await database.Courses
            .Include(course => course.StudyMode)
            .Where(course =>
                course.IsActive &&
                (course.SemesterOffered == "Both" ||
                 course.SemesterOffered == settings.Semester))
            .OrderBy(course => course.Code)
            .ToListAsync();

        var lecturers = await database.Lecturers
            .Where(lecturer => lecturer.IsActive)
            .ToListAsync();

        var rooms = await database.Rooms
            .Where(room => room.IsActive)
            .ToListAsync();

        var assignments = await database.CourseLecturerAssignments
            .Where(assignment => assignment.IsActive)
            .ToListAsync();

        var scheduledCourses = new List<string>();
        var unresolvedCourses = new List<string>();
        var unassignedCourses = new List<string>();

        foreach (var course in courses.Where(course =>
                     entries.All(entry => entry.CourseId != course.Id)))
        {
            var assignedLecturerIds = assignments
                .Where(assignment => assignment.CourseId == course.Id)
                .OrderByDescending(assignment => assignment.IsPrimary)
                .Select(assignment => assignment.LecturerId)
                .ToHashSet();

            var eligibleLecturers = lecturers
                .Where(lecturer => assignedLecturerIds.Contains(lecturer.Id))
                .ToList();

            if (eligibleLecturers.Count == 0)
            {
                unassignedCourses.Add(course.Code);
                continue;
            }

            var suggestion = timetableService.SuggestEntry(
                course,
                eligibleLecturers,
                rooms,
                entries,
                registrations,
                settings);

            if (suggestion is null)
            {
                unresolvedCourses.Add(course.Code);
                continue;
            }

            database.TimetableEntries.Add(suggestion);
            entries.Add(suggestion);

            course.ScheduleKey = suggestion.ScheduleLabel;
            course.Venue = suggestion.Room.Code;

            scheduledCourses.Add(course.Code);
        }

        if (scheduledCourses.Count > 0)
        {
            await database.SaveChangesAsync();
        }

        TempData["Success"] = scheduledCourses.Count == 0
            ? "No new draft allocations could be generated."
            : $"Draft allocations were generated for: {string.Join(", ", scheduledCourses)}.";

        if (unassignedCourses.Count > 0)
        {
            TempData["Error"] =
                $"No lecturer is assigned to: {string.Join(", ", unassignedCourses)}.";
        }
        else if (unresolvedCourses.Count > 0)
        {
            TempData["Error"] =
                $"No conflict-free allocation was found for: {string.Join(", ", unresolvedCourses)}.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteDraftAsync()
    {
        var settings = await settingsService.GetAsync();

        var entry = await database.TimetableEntries
            .SingleOrDefaultAsync(item =>
                item.Id == EntryId &&
                item.AcademicYear == settings.AcademicYear &&
                item.Semester == settings.Semester &&
                item.Status == "Draft");

        if (entry is null)
        {
            TempData["Error"] = "The draft allocation could not be found.";
            return RedirectToPage();
        }

        database.TimetableEntries.Remove(entry);
        await database.SaveChangesAsync();

        TempData["Success"] =
            "Draft allocation removed. You can now generate or create a replacement.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPublishAsync()
    {
        var settings = await settingsService.GetAsync();
        Settings = settings;

        var entries = await GetEntriesAsync();
        var registrations = await GetCurrentRegistrationsAsync();

        var issues = timetableService.FindIssues(entries, registrations);

        if (issues.Count > 0)
        {
            TempData["Error"] =
                "The timetable cannot be published while constraint issues exist. " +
                "Review the timetable health check first.";

            return RedirectToPage();
        }

        var drafts = entries
            .Where(entry => entry.Status == "Draft")
            .ToList();

        if (drafts.Count == 0)
        {
            TempData["Error"] = "There are no draft allocations waiting for publication.";
            return RedirectToPage();
        }

        foreach (var entry in drafts)
        {
            entry.Status = "Published";
        }

        await database.SaveChangesAsync();

        TempData["Success"] =
            $"{drafts.Count} approved timetable allocation(s) were published.";

        return RedirectToPage();
    }

    private async Task LoadPageAsync()
    {
        Settings = await settingsService.GetAsync();

        Courses = await database.Courses
            .Include(course => course.StudyMode)
            .Where(course =>
                course.IsActive &&
                (course.SemesterOffered == "Both" ||
                 course.SemesterOffered == Settings.Semester))
            .AsNoTracking()
            .OrderBy(course => course.Code)
            .ToListAsync();

        Lecturers = await database.Lecturers
            .Where(lecturer => lecturer.IsActive)
            .AsNoTracking()
            .OrderBy(lecturer => lecturer.FullName)
            .ToListAsync();

        CourseLecturerAssignments = await database.CourseLecturerAssignments
            .Where(assignment => assignment.IsActive)
            .AsNoTracking()
            .ToListAsync();

        Rooms = await database.Rooms
            .Where(room => room.IsActive)
            .AsNoTracking()
            .OrderBy(room => room.Code)
            .ToListAsync();

        var entries = await GetEntriesAsync();
        var registrations = await GetCurrentRegistrationsAsync();

        Entries = entries
            .OrderBy(entry => DayOrder(entry.DayOfWeek))
            .ThenBy(entry => entry.StartMinutes)
            .ThenBy(entry => entry.Course.Code)
            .ToList();

        AwaitingCourses = Courses
            .Where(course => entries.All(entry => entry.CourseId != course.Id))
            .ToList();

        Issues = timetableService.FindIssues(entries, registrations);
    }

    private async Task<List<TimetableEntry>> GetEntriesAsync() =>
        await database.TimetableEntries
            .Include(entry => entry.Course)
                .ThenInclude(course => course.StudyMode)
            .Include(entry => entry.Lecturer)
            .Include(entry => entry.Room)
            .Where(entry =>
                entry.AcademicYear == Settings.AcademicYear &&
                entry.Semester == Settings.Semester)
            .ToListAsync();

    private async Task<List<CourseRegistration>> GetCurrentRegistrationsAsync() =>
        await database.CourseRegistrations
            .Where(registration =>
                registration.AcademicYear == Settings.AcademicYear &&
                registration.Semester == Settings.Semester &&
                registration.Status == "Confirmed")
            .ToListAsync();

    private static bool TryGetSlot(string value, out TimetableSlot slot)
    {
        slot = TimetableService.StandardSlots.First();

        var match = TimetableService.StandardSlots
            .SingleOrDefault(item => item.Value == value);

        if (match is null)
        {
            return false;
        }

        slot = match;
        return true;
    }

    private static int DayOrder(string day) => day switch
    {
        "Monday" => 1,
        "Tuesday" => 2,
        "Wednesday" => 3,
        "Thursday" => 4,
        "Friday" => 5,
        _ => 99
    };

    private static bool IsOfferedThisSemester(Course course, string semester) =>
        course.SemesterOffered.Equals("Both", StringComparison.OrdinalIgnoreCase) ||
        course.SemesterOffered.Equals(semester, StringComparison.OrdinalIgnoreCase);
}