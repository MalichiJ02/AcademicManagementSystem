using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Coordinator;

[Authorize(Roles = "Coordinator,Administrator")]
public class TimetableModel(
    AcademicManagementDbContext database,
    TimetableService timetableService,
    AcademicSettingsService settingsService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public int CourseId { get; set; }

    [BindProperty]
    public int? LecturerId { get; set; }

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
    public IReadOnlyList<CourseLecturerAssignment> CourseLecturerAssignments { get; private set; } = [];
    public AcademicSettings Settings { get; private set; } = new();

    public IReadOnlyList<Lecturer> EligibleLecturersFor(int courseId)
    {
        var lecturerIds = CourseLecturerAssignments
            .Where(assignment => assignment.CourseId == courseId)
            .Select(assignment => assignment.LecturerId)
            .ToHashSet();

        return Lecturers.Where(lecturer => lecturerIds.Contains(lecturer.Id)).ToList();
    }

    public IReadOnlyList<TimetableSlot> Slots => TimetableService.StandardSlots;

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadPageAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        var scope = await GetScopeAsync();

        if (!scope.IsAllowed)
        {
            TempData["Error"] =
                "Your coordinator account is not assigned to a school. Contact an administrator.";
            return RedirectToPage();
        }

        Settings = await settingsService.GetAsync();

        if (!TryGetSlot(SlotValue, out var slot))
        {
            TempData["Error"] = "Choose one of the available timetable periods.";
            return RedirectToPage();
        }

        var course = await database.Courses
            .Include(item => item.StudyMode)
            .Include(item => item.Programme)
            .SingleOrDefaultAsync(item => item.Id == CourseId && item.IsActive);

        var room = await database.Rooms
            .SingleOrDefaultAsync(item => item.Id == RoomId && item.IsActive);

        if (course is null || room is null)
        {
            TempData["Error"] = "Select a valid active course and room.";
            return RedirectToPage();
        }

        if (!CanManageCourse(course, scope))
        {
            TempData["Error"] = "You may schedule only courses assigned to your school.";
            return RedirectToPage();
        }

        if (!IsOfferedThisSemester(course, Settings.Semester))
        {
            TempData["Error"] = $"{course.Code} is not offered in {Settings.Semester}.";
            return RedirectToPage();
        }

        if (!TimetableService.IsSlotCompatibleWithCourse(course, slot))
        {
            TempData["Error"] =
                TimetableService.IsEveningCourse(course)
                    ? $"{course.Code} must use the 18:00–20:00 evening period."
                    : $"{course.Code} must use a daytime period between 08:00 and 18:00.";

            return RedirectToPage();
        }

        await using var transaction = await database.Database.BeginTransactionAsync();
        var entries = await GetEntriesAsync();
        var existingCourseSessions = entries
            .Where(entry => entry.CourseId == course.Id)
            .ToList();

        if (existingCourseSessions.Count >= course.SessionsPerWeek)
        {
            TempData["Error"] =
                $"{course.Code} already has all {course.SessionsPerWeek} required weekly session(s).";

            return RedirectToPage();
        }

        var lecturer = LecturerId is int lecturerId
            ? await FindEligibleLecturerAsync(course.Id, lecturerId)
            : null;

        if (ModelState.GetFieldValidationState(nameof(LecturerId)) == ModelValidationState.Invalid ||
            (LecturerId.HasValue && lecturer is null))
        {
            TempData["Error"] = $"Select an eligible active lecturer for {course.Code}, or leave the lecturer pending.";
            return RedirectToPage();
        }

        var registrations = await GetCurrentRegistrationsAsync();

        var candidate = new TimetableEntry
        {
            CourseId = course.Id,
            Course = course,
            LecturerId = lecturer?.Id,
            Lecturer = lecturer,
            RoomId = room.Id,
            Room = room,
            AcademicYear = Settings.AcademicYear,
            Semester = Settings.Semester,
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
        entries.Add(candidate);

        UpdateCourseScheduleSummary(course, entries);

        await database.SaveChangesAsync();

        await transaction.CommitAsync();

        TempData["Success"] =
            $"{course.Code} session {existingCourseSessions.Count + 1} of " +
            $"{course.SessionsPerWeek} was scheduled for {candidate.ScheduleLabel}.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostGenerateAsync()
    {
        var scope = await GetScopeAsync();

        if (!scope.IsAllowed)
        {
            TempData["Error"] =
                "Your coordinator account is not assigned to a school. Contact an administrator.";
            return RedirectToPage();
        }

        Settings = await settingsService.GetAsync();

        await using var transaction = await database.Database.BeginTransactionAsync();
        var entries = await GetEntriesAsync();
        var registrations = await GetCurrentRegistrationsAsync();

        var coursesQuery = database.Courses
            .Include(course => course.StudyMode)
            .Include(course => course.Programme)
            .Where(course =>
                course.IsActive &&
                (course.SemesterOffered == "Both" ||
                 course.SemesterOffered == Settings.Semester));

        if (!scope.IsAdministrator)
        {
            coursesQuery = coursesQuery.Where(course =>
                course.Programme != null &&
                course.Programme.SchoolId == scope.SchoolId);
        }

        var courses = await coursesQuery
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

        foreach (var course in courses)
        {
            var currentSessionCount = entries.Count(entry =>
                entry.CourseId == course.Id);

            var sessionsToAdd = course.SessionsPerWeek - currentSessionCount;

            if (sessionsToAdd <= 0)
            {
                continue;
            }

            var eligibleLecturerIds = assignments
                .Where(assignment => assignment.CourseId == course.Id)
                .Select(assignment => assignment.LecturerId)
                .ToHashSet();

            var eligibleLecturers = lecturers
                .Where(lecturer => eligibleLecturerIds.Contains(lecturer.Id))
                .ToList();

            var suggestions = timetableService.SuggestEntries(
                course,
                eligibleLecturers,
                rooms,
                entries,
                registrations,
                Settings,
                sessionsToAdd);

            if (suggestions.Count != sessionsToAdd)
            {
                unresolvedCourses.Add(
                    $"{course.Code} ({sessionsToAdd} session(s) could not be allocated)");

                continue;
            }

            foreach (var suggestion in suggestions)
            {
                suggestion.Status = "Draft";
                database.TimetableEntries.Add(suggestion);
                entries.Add(suggestion);
            }

            UpdateCourseScheduleSummary(course, entries);

            scheduledCourses.Add(
                $"{course.Code} ({suggestions.Count} session(s))");
        }

        if (scheduledCourses.Count > 0)
        {
            await database.SaveChangesAsync();
        }

        await transaction.CommitAsync();

        TempData["Success"] = scheduledCourses.Count == 0
            ? "All active courses already have their required weekly sessions, or none could be allocated."
            : $"Draft allocations were created for {string.Join(", ", scheduledCourses)}.";

        if (unresolvedCourses.Count > 0)
        {
            TempData["Error"] =
                $"No complete conflict-free allocation was found for: " +
                $"{string.Join(", ", unresolvedCourses)}.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAssignLecturerAsync(int assignedLecturerId)
    {
        var scope = await GetScopeAsync();
        Settings = await settingsService.GetAsync();

        await using var transaction = await database.Database.BeginTransactionAsync();
        var entries = await GetEntriesAsync();
        var entry = entries.SingleOrDefault(item => item.Id == EntryId);

        if (entry is null || !CanManageCourse(entry.Course, scope))
        {
            TempData["Error"] = "This timetable entry is unavailable for your account in the current term.";
            return RedirectToPage();
        }

        if (entry.LecturerId.HasValue)
        {
            TempData["Error"] = "This session already has a lecturer.";
            return RedirectToPage();
        }

        var lecturer = await FindEligibleLecturerAsync(entry.CourseId, assignedLecturerId);

        if (lecturer is null)
        {
            TempData["Error"] = $"Select an eligible active lecturer for {entry.Course.Code}.";
            return RedirectToPage();
        }

        entry.LecturerId = lecturer.Id;
        entry.Lecturer = lecturer;
        var registrations = await GetCurrentRegistrationsAsync();
        var validation = timetableService.Validate(entry, entries, registrations);

        if (!validation.IsValid)
        {
            TempData["Error"] = string.Join(" ", validation.Errors);
            return RedirectToPage();
        }

        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = $"{lecturer.FullName} was assigned to {entry.Course.Code} at {entry.ScheduleLabel}.";
        return RedirectToPage();
    }

    private Task<Lecturer?> FindEligibleLecturerAsync(int courseId, int lecturerId) =>
        database.Lecturers.SingleOrDefaultAsync(lecturer =>
            lecturer.Id == lecturerId &&
            lecturer.IsActive &&
            database.CourseLecturerAssignments.Any(assignment =>
                assignment.CourseId == courseId &&
                assignment.LecturerId == lecturer.Id &&
                assignment.IsActive));

    public async Task<IActionResult> OnPostDeleteDraftAsync()
    {
        var scope = await GetScopeAsync();

        if (!scope.IsAllowed)
        {
            TempData["Error"] =
                "Your coordinator account is not assigned to a school. Contact an administrator.";
            return RedirectToPage();
        }

        var entry = await database.TimetableEntries
            .Include(item => item.Course)
                .ThenInclude(course => course.Programme)
            .SingleOrDefaultAsync(item => item.Id == EntryId);

        if (entry is null)
        {
            TempData["Error"] = "Timetable entry not found.";
            return RedirectToPage();
        }

        if (!CanManageCourse(entry.Course, scope))
        {
            TempData["Error"] = "You may manage only courses assigned to your school.";
            return RedirectToPage();
        }

        if (!entry.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Only draft timetable entries can be removed.";
            return RedirectToPage();
        }

        database.TimetableEntries.Remove(entry);
        await database.SaveChangesAsync();

        var remainingEntries = await GetEntriesAsync();
        UpdateCourseScheduleSummary(entry.Course, remainingEntries);
        await database.SaveChangesAsync();

        TempData["Success"] = $"{entry.Course.Code} draft allocation was removed.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAllDraftsAsync(string academicYear, string semester)
    {
        var scope = await GetScopeAsync();
        if (!scope.IsAllowed)
        {
            TempData["Error"] =
                "Your coordinator account is not assigned to a school. Contact an administrator.";
            return RedirectToPage();
        }

        Settings = await settingsService.GetAsync();
        if (academicYear != Settings.AcademicYear || semester != Settings.Semester)
        {
            TempData["Error"] = "The active term changed. Review the timetable before deleting drafts.";
            return RedirectToPage();
        }

        await using var transaction = await database.Database.BeginTransactionAsync();
        var entries = await GetEntriesAsync();
        var drafts = entries.Where(entry =>
            entry.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase) &&
            CanManageCourse(entry.Course, scope)).ToHashSet();

        if (drafts.Count == 0)
        {
            TempData["Success"] = "There are no timetable drafts to delete in this term.";
            return RedirectToPage();
        }

        var remainingEntries = entries.Where(entry => !drafts.Contains(entry)).ToList();
        database.TimetableEntries.RemoveRange(drafts);

        foreach (var course in drafts.Select(entry => entry.Course).DistinctBy(course => course.Id))
        {
            UpdateCourseScheduleSummary(course, remainingEntries);
        }

        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = $"Deleted {drafts.Count} timetable draft session(s).";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPublishAsync()
    {
        var scope = await GetScopeAsync();

        if (!scope.IsAllowed)
        {
            TempData["Error"] =
                "Your coordinator account is not assigned to a school. Contact an administrator.";
            return RedirectToPage();
        }

        Settings = await settingsService.GetAsync();

        await using var transaction = await database.Database.BeginTransactionAsync();
        var entries = await GetEntriesAsync();
        var registrations = await GetCurrentRegistrationsAsync();

        var drafts = entries
            .Where(entry =>
                entry.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase) &&
                CanManageCourse(entry.Course, scope))
            .ToList();

        if (drafts.Count == 0)
        {
            TempData["Error"] = "There are no draft entries for your school to publish.";
            return RedirectToPage();
        }

        var incompleteCourses = drafts
            .Select(entry => entry.Course)
            .DistinctBy(course => course.Id)
            .Where(course =>
                entries.Count(entry => entry.CourseId == course.Id) <
                course.SessionsPerWeek)
            .Select(course =>
                $"{course.Code} ({entries.Count(entry => entry.CourseId == course.Id)}/{course.SessionsPerWeek})")
            .ToList();

        if (incompleteCourses.Count > 0)
        {
            TempData["Error"] =
                "Each course must have all required weekly sessions before publishing. " +
                $"Incomplete: {string.Join(", ", incompleteCourses)}.";

            return RedirectToPage();
        }

        var issues = timetableService.FindIssues(entries, registrations);

        if (issues.Count > 0)
        {
            TempData["Error"] =
                "The timetable contains unresolved conflicts. Review and correct draft entries before publishing.";

            return RedirectToPage();
        }

        foreach (var draft in drafts)
        {
            draft.Status = "Published";
        }

        await database.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] =
            $"{drafts.Count} timetable session(s) were published for your school.";

        return RedirectToPage();
    }

    private async Task LoadPageAsync()
    {
        Settings = await settingsService.GetAsync();

        var scope = await GetScopeAsync();

        if (!scope.IsAllowed)
        {
            TempData["Error"] =
                "Your coordinator account is not assigned to a school. Contact an administrator.";

            Courses = [];
            AwaitingCourses = [];
            Lecturers = [];
            Rooms = [];
            Entries = [];
            Issues = [];
            CourseLecturerAssignments = [];

            return;
        }

        var coursesQuery = database.Courses
            .Include(course => course.StudyMode)
            .Include(course => course.Programme)
            .Where(course =>
                course.IsActive &&
                (course.SemesterOffered == "Both" ||
                 course.SemesterOffered == Settings.Semester));

        if (!scope.IsAdministrator)
        {
            coursesQuery = coursesQuery.Where(course =>
                course.Programme != null &&
                course.Programme.SchoolId == scope.SchoolId);
        }

        Courses = await coursesQuery
            .AsNoTracking()
            .OrderBy(course => course.Code)
            .ToListAsync();

        var allEntries = await GetEntriesAsync();
        var visibleEntries = allEntries
            .Where(entry => CanManageCourse(entry.Course, scope))
            .ToList();

        var visibleCourseIds = Courses.Select(course => course.Id)
            .Union(visibleEntries.Select(entry => entry.CourseId))
            .ToList();

        CourseLecturerAssignments = await database.CourseLecturerAssignments
            .Where(assignment =>
                assignment.IsActive &&
                visibleCourseIds.Contains(assignment.CourseId))
            .AsNoTracking()
            .ToListAsync();

        Lecturers = await database.Lecturers
            .Where(lecturer => lecturer.IsActive)
            .AsNoTracking()
            .OrderBy(lecturer => lecturer.FullName)
            .ToListAsync();

        Rooms = await database.Rooms
            .Where(room => room.IsActive)
            .AsNoTracking()
            .OrderBy(room => room.Code)
            .ToListAsync();

        var registrations = await GetCurrentRegistrationsAsync();

        Entries = visibleEntries
            .OrderBy(entry => DayOrder(entry.DayOfWeek))
            .ThenBy(entry => entry.StartMinutes)
            .ThenBy(entry => entry.Course.Code)
            .ToList();

        AwaitingCourses = Courses
            .Where(course =>
                allEntries.Count(entry => entry.CourseId == course.Id) <
                course.SessionsPerWeek)
            .ToList();

        Issues = timetableService.FindIssues(visibleEntries, registrations);
    }

    private async Task<List<TimetableEntry>> GetEntriesAsync() =>
        await database.TimetableEntries
            .Include(entry => entry.Course)
                .ThenInclude(course => course.StudyMode)
            .Include(entry => entry.Course)
                .ThenInclude(course => course.Programme)
            .Include(entry => entry.Lecturer)
            .Include(entry => entry.Room)
            .Where(entry =>
                entry.AcademicYear == Settings.AcademicYear &&
                entry.Semester == Settings.Semester)
            .ToListAsync();

    private async Task<List<CourseRegistration>> GetCurrentRegistrationsAsync() =>
        await database.CourseRegistrations
            .Include(registration => registration.Course)
            .Where(registration =>
                registration.AcademicYear == Settings.AcademicYear &&
                registration.Semester == Settings.Semester &&
                registration.Status == "Confirmed")
            .ToListAsync();

    private async Task<CoordinatorScope> GetScopeAsync()
    {
        if (User.IsInRole("Administrator"))
        {
            return new CoordinatorScope(true, true, null);
        }

        var user = await userManager.GetUserAsync(User);

        if (user?.CoordinatingSchoolId is null)
        {
            return new CoordinatorScope(false, false, null);
        }

        return new CoordinatorScope(true, false, user.CoordinatingSchoolId);
    }

    private static bool CanManageCourse(
        Course course,
        CoordinatorScope scope) =>
        scope.IsAdministrator ||
        (scope.SchoolId.HasValue &&
         course.Programme?.SchoolId == scope.SchoolId.Value);

    private static void UpdateCourseScheduleSummary(
        Course course,
        IReadOnlyCollection<TimetableEntry> entries)
    {
        var courseEntries = entries
            .Where(entry => entry.CourseId == course.Id)
            .OrderBy(entry => DayOrder(entry.DayOfWeek))
            .ThenBy(entry => entry.StartMinutes)
            .ToList();

        if (courseEntries.Count == 0)
        {
            course.ScheduleKey = string.Empty;
            course.Venue = string.Empty;
            return;
        }

        if (courseEntries.Count == 1)
        {
            course.ScheduleKey = courseEntries[0].ScheduleLabel;
            course.Venue = courseEntries[0].Room.Code;
            return;
        }

        course.ScheduleKey = $"{courseEntries.Count} weekly sessions";
        course.Venue = "See timetable";
    }

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

    private sealed record CoordinatorScope(
        bool IsAllowed,
        bool IsAdministrator,
        int? SchoolId);
}