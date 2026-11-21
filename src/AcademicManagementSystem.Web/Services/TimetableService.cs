using AcademicManagementSystem.Web.Models;

namespace AcademicManagementSystem.Web.Services;

public sealed class TimetableService
{
    public static readonly IReadOnlyList<TimetableSlot> StandardSlots =
    [
        new("Monday", 480, 600),
        new("Monday", 600, 720),
        new("Monday", 720, 840),
        new("Monday", 840, 960),
        new("Monday", 960, 1080),
        new("Monday", 1080, 1200),

        new("Tuesday", 480, 600),
        new("Tuesday", 600, 720),
        new("Tuesday", 720, 840),
        new("Tuesday", 840, 960),
        new("Tuesday", 960, 1080),
        new("Tuesday", 1080, 1200),

        new("Wednesday", 480, 600),
        new("Wednesday", 600, 720),
        new("Wednesday", 720, 840),
        new("Wednesday", 840, 960),
        new("Wednesday", 960, 1080),
        new("Wednesday", 1080, 1200),

        new("Thursday", 480, 600),
        new("Thursday", 600, 720),
        new("Thursday", 720, 840),
        new("Thursday", 840, 960),
        new("Thursday", 960, 1080),
        new("Thursday", 1080, 1200),

        new("Friday", 480, 600),
        new("Friday", 600, 720),
        new("Friday", 720, 840),
        new("Friday", 840, 960),
        new("Friday", 960, 1080),
        new("Friday", 1080, 1200)
    ];

    public TimetableValidationResult Validate(
        TimetableEntry candidate,
        IReadOnlyCollection<TimetableEntry> existingEntries,
        IReadOnlyCollection<CourseRegistration> registrations)
    {
        var errors = new List<string>();

        if (candidate.EndMinutes <= candidate.StartMinutes)
        {
            errors.Add("The end time must be later than the start time.");
        }

        if (!IsSlotCompatibleWithCourse(
                candidate.Course,
                new TimetableSlot(
                    candidate.DayOfWeek,
                    candidate.StartMinutes,
                    candidate.EndMinutes)))
        {
            errors.Add(
                $"{candidate.Course.Code} is not scheduled in a teaching period compatible with its study mode.");
        }

        var requiredCapacity = RequiredCapacity(
            candidate.CourseId,
            candidate.Course.ExpectedEnrollment,
            registrations);

        if (candidate.Room.Capacity < requiredCapacity)
        {
            errors.Add(
                $"{candidate.Room.Code} holds {candidate.Room.Capacity}, but {candidate.Course.Code} " +
                $"requires capacity for {requiredCapacity} student(s).");
        }

        if (candidate.Lecturer is { } lecturer && !lecturer.IsAvailableOn(candidate.DayOfWeek))
        {
            errors.Add($"{lecturer.FullName} is not available on {candidate.DayOfWeek}.");
        }

        foreach (var entry in existingEntries.Where(entry =>
                     !ReferenceEquals(entry, candidate) &&
                     (candidate.Id == 0 || entry.Id != candidate.Id)))
        {
            if (candidate.CourseId == entry.CourseId &&
                candidate.DayOfWeek.Equals(
                    entry.DayOfWeek,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"{candidate.Course.Code} already has a weekly session on {candidate.DayOfWeek}. " +
                    "Its sessions must be placed on different days.");
            }

            if (!Overlaps(candidate, entry))
            {
                continue;
            }

            if (candidate.RoomId == entry.RoomId)
            {
                errors.Add(
                    $"Room conflict: {candidate.Room.Code} is already allocated to {entry.Course.Code} " +
                    $"at {entry.ScheduleLabel}.");
            }

            if (candidate.LecturerId.HasValue && candidate.LecturerId == entry.LecturerId)
            {
                errors.Add(
                    $"Lecturer conflict: {candidate.Lecturer?.FullName ?? "The assigned lecturer"} is already assigned to {entry.Course.Code} " +
                    $"at {entry.ScheduleLabel}.");
            }

            var conflict = AnalyseStudentConflicts(candidate, entry, registrations);

            if (conflict.NormalConflictCount > 0)
            {
                errors.Add(
                    $"Normal student timetable conflict: {conflict.NormalConflictCount} registered student(s) " +
                    $"take both {candidate.Course.Code} and {entry.Course.Code}.");
            }
        }

        return new TimetableValidationResult(
            errors.Count == 0,
            errors.Distinct().ToList());
    }

    public IReadOnlyList<TimetableIssue> FindIssues(
        IReadOnlyCollection<TimetableEntry> entries,
        IReadOnlyCollection<CourseRegistration> registrations)
    {
        var issues = new List<TimetableIssue>();

        foreach (var entry in entries)
        {
            if (!IsSlotCompatibleWithCourse(
                    entry.Course,
                    new TimetableSlot(
                        entry.DayOfWeek,
                        entry.StartMinutes,
                        entry.EndMinutes)))
            {
                issues.Add(new TimetableIssue(
                    "Study-mode schedule",
                    $"{entry.Course.Code} is not scheduled in a period compatible with its study mode.",
                    [entry.Id]));
            }

            var requiredCapacity = RequiredCapacity(
                entry.CourseId,
                entry.Course.ExpectedEnrollment,
                registrations);

            if (entry.Room.Capacity < requiredCapacity)
            {
                issues.Add(new TimetableIssue(
                    "Capacity",
                    $"{entry.Course.Code} requires capacity for {requiredCapacity}, but " +
                    $"{entry.Room.Code} holds {entry.Room.Capacity}.",
                    [entry.Id]));
            }

            if (entry.Lecturer is { } lecturer && !lecturer.IsAvailableOn(entry.DayOfWeek))
            {
                issues.Add(new TimetableIssue(
                    "Availability",
                    $"{lecturer.FullName} is unavailable on {entry.DayOfWeek} for {entry.Course.Code}.",
                    [entry.Id]));
            }
        }

        for (var firstIndex = 0; firstIndex < entries.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < entries.Count; secondIndex++)
            {
                var first = entries.ElementAt(firstIndex);
                var second = entries.ElementAt(secondIndex);

                if (first.CourseId == second.CourseId &&
                    first.DayOfWeek.Equals(
                        second.DayOfWeek,
                        StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new TimetableIssue(
                        "Course session spacing",
                        $"{first.Course.Code} has more than one weekly session on " +
                        $"{first.DayOfWeek}. Sessions should be on different days.",
                        [first.Id, second.Id]));
                }

                if (!Overlaps(first, second))
                {
                    continue;
                }

                if (first.RoomId == second.RoomId)
                {
                    issues.Add(new TimetableIssue(
                        "Room conflict",
                        $"{first.Room.Code} is double-booked for {first.Course.Code} and " +
                        $"{second.Course.Code} at {first.ScheduleLabel}.",
                        [first.Id, second.Id]));
                }

                if (first.LecturerId.HasValue && first.LecturerId == second.LecturerId)
                {
                    issues.Add(new TimetableIssue(
                        "Lecturer conflict",
                        $"{first.Lecturer?.FullName ?? "The assigned lecturer"} is double-booked for {first.Course.Code} and " +
                        $"{second.Course.Code} at {first.ScheduleLabel}.",
                        [first.Id, second.Id]));
                }

                var conflict = AnalyseStudentConflicts(first, second, registrations);

                if (conflict.NormalConflictCount > 0)
                {
                    issues.Add(new TimetableIssue(
                        "Student conflict",
                        $"{conflict.NormalConflictCount} normal registered student(s) have an overlap " +
                        $"between {first.Course.Code} and {second.Course.Code}.",
                        [first.Id, second.Id]));
                }

                if (conflict.CarryConflictCount > 0)
                {
                    issues.Add(new TimetableIssue(
                        "Carry-course conflict",
                        $"{conflict.CarryConflictCount} carry-course student(s) have an overlap " +
                        $"between {first.Course.Code} and {second.Course.Code}.",
                        [first.Id, second.Id]));
                }
            }
        }

        return issues;
    }

    public TimetableEntry? SuggestEntry(
        Course course,
        IReadOnlyCollection<Lecturer> lecturers,
        IReadOnlyCollection<Room> rooms,
        IReadOnlyCollection<TimetableEntry> existingEntries,
        IReadOnlyCollection<CourseRegistration> registrations)
    {
        return SuggestEntry(
            course,
            lecturers,
            rooms,
            existingEntries,
            registrations,
            new AcademicSettings());
    }

    public TimetableEntry? SuggestEntry(
        Course course,
        IReadOnlyCollection<Lecturer> lecturers,
        IReadOnlyCollection<Room> rooms,
        IReadOnlyCollection<TimetableEntry> existingEntries,
        IReadOnlyCollection<CourseRegistration> registrations,
        AcademicSettings settings)
    {
        return FindBestCandidate(
            course,
            lecturers,
            rooms,
            existingEntries,
            registrations,
            settings);
    }

    /// <summary>
    /// Creates the requested number of sessions for one course. Every generated
    /// session uses a different day, and all previously generated sessions are
    /// considered when selecting the next one.
    /// </summary>
    public IReadOnlyList<TimetableEntry> SuggestEntries(
        Course course,
        IReadOnlyCollection<Lecturer> lecturers,
        IReadOnlyCollection<Room> rooms,
        IReadOnlyCollection<TimetableEntry> existingEntries,
        IReadOnlyCollection<CourseRegistration> registrations,
        AcademicSettings settings,
        int sessionsToAdd)
    {
        if (sessionsToAdd <= 0)
        {
            return [];
        }

        var suggestions = new List<TimetableEntry>();
        var workingEntries = existingEntries.ToList();
        int? selectedLecturerId = null;

        for (var sessionNumber = 1; sessionNumber <= sessionsToAdd; sessionNumber++)
        {
            var eligibleLecturers = selectedLecturerId.HasValue
                ? lecturers
                    .Where(lecturer => lecturer.Id == selectedLecturerId.Value)
                    .ToList()
                : lecturers;

            var suggestion = FindBestCandidate(
                course,
                eligibleLecturers,
                rooms,
                workingEntries,
                registrations,
                settings);

            // Do not create a partial timetable for a course. The coordinator
            // must either receive all required sessions or none.
            if (suggestion is null)
            {
                return [];
            }

            selectedLecturerId ??= suggestion.LecturerId;

            suggestions.Add(suggestion);
            workingEntries.Add(suggestion);
        }

        return suggestions;
    }

    public static bool IsEveningSlot(TimetableSlot slot) => slot.StartMinutes >= 1080;

    public static bool IsEveningCourse(Course course) =>
        course.StudyMode?.Name.Equals("Evening", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsSlotCompatibleWithCourse(Course course, TimetableSlot slot) =>
        IsEveningCourse(course) == IsEveningSlot(slot);

    private static TimetableEntry? FindBestCandidate(
        Course course,
        IEnumerable<Lecturer> lecturers,
        IReadOnlyCollection<Room> rooms,
        IReadOnlyCollection<TimetableEntry> existingEntries,
        IReadOnlyCollection<CourseRegistration> registrations,
        AcademicSettings settings)
    {
        TimetableEntry? bestCandidate = null;
        var bestScore = int.MaxValue;
        var lecturerChoices = lecturers.Where(lecturer => lecturer.IsActive)
            .Cast<Lecturer?>().DefaultIfEmpty().ToList();

        foreach (var slot in StandardSlots
                     .Where(slot => IsSlotCompatibleWithCourse(course, slot)))
        {
            // A course can have two sessions per week, but not two sessions
            // on the same weekday.
            if (existingEntries.Any(entry =>
                    entry.CourseId == course.Id &&
                    entry.DayOfWeek.Equals(
                        slot.DayOfWeek,
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            foreach (var lecturer in lecturerChoices.Where(lecturer =>
                         lecturer?.IsAvailableOn(slot.DayOfWeek) != false))
            {
                foreach (var room in rooms
                             .Where(room =>
                                 room.IsActive &&
                                 room.Capacity >= RequiredCapacity(
                                     course.Id,
                                     course.ExpectedEnrollment,
                                     registrations))
                             .OrderBy(room => room.Capacity))
                {
                    var candidate = new TimetableEntry
                    {
                        CourseId = course.Id,
                        Course = course,
                        LecturerId = lecturer?.Id,
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

                    if (!PassesHardConstraints(
                            candidate,
                            existingEntries,
                            registrations))
                    {
                        continue;
                    }

                    var score = ScoreStudentConflicts(
                        candidate,
                        existingEntries,
                        registrations);

                    if (score < bestScore)
                    {
                        bestCandidate = candidate;
                        bestScore = score;

                        if (score == 0)
                        {
                            return bestCandidate;
                        }
                    }
                }
            }
        }

        return bestCandidate;
    }

    private static bool PassesHardConstraints(
        TimetableEntry candidate,
        IReadOnlyCollection<TimetableEntry> existingEntries,
        IReadOnlyCollection<CourseRegistration> registrations)
    {
        if (candidate.EndMinutes <= candidate.StartMinutes ||
            !IsSlotCompatibleWithCourse(
                candidate.Course,
                new TimetableSlot(
                    candidate.DayOfWeek,
                    candidate.StartMinutes,
                    candidate.EndMinutes)) ||
            candidate.Room.Capacity < RequiredCapacity(
                candidate.CourseId,
                candidate.Course.ExpectedEnrollment,
                registrations) ||
            candidate.Lecturer?.IsAvailableOn(candidate.DayOfWeek) == false)
        {
            return false;
        }

        foreach (var entry in existingEntries.Where(entry =>
                     !ReferenceEquals(entry, candidate) &&
                     (candidate.Id == 0 || entry.Id != candidate.Id)))
        {
            if (entry.CourseId == candidate.CourseId &&
                entry.DayOfWeek.Equals(
                    candidate.DayOfWeek,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!Overlaps(candidate, entry))
            {
                continue;
            }

            if (entry.RoomId == candidate.RoomId ||
                (candidate.LecturerId.HasValue && entry.LecturerId == candidate.LecturerId))
            {
                return false;
            }

            var conflict = AnalyseStudentConflicts(candidate, entry, registrations);

            if (conflict.NormalConflictCount > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static int ScoreStudentConflicts(
        TimetableEntry candidate,
        IReadOnlyCollection<TimetableEntry> existingEntries,
        IReadOnlyCollection<CourseRegistration> registrations)
    {
        var carryConflicts = 0;

        foreach (var entry in existingEntries.Where(entry =>
                     Overlaps(candidate, entry)))
        {
            var conflict = AnalyseStudentConflicts(candidate, entry, registrations);

            // Normal conflicts are already rejected by PassesHardConstraints.
            carryConflicts += conflict.CarryConflictCount;
        }

        return carryConflicts;
    }

    private static int RequiredCapacity(
        int courseId,
        int expectedEnrollment,
        IReadOnlyCollection<CourseRegistration> registrations)
    {
        var confirmedCount = registrations
            .Where(registration =>
                registration.Status == "Confirmed" &&
                registration.CourseId == courseId)
            .Select(registration => registration.StudentId)
            .Distinct()
            .Count();

        return Math.Max(expectedEnrollment, confirmedCount);
    }

    private static StudentConflictSummary AnalyseStudentConflicts(
        TimetableEntry first,
        TimetableEntry second,
        IReadOnlyCollection<CourseRegistration> registrations)
    {
        var firstRegistrations = registrations
            .Where(registration =>
                registration.Status == "Confirmed" &&
                registration.CourseId == first.CourseId)
            .ToList();

        var secondRegistrations = registrations
            .Where(registration =>
                registration.Status == "Confirmed" &&
                registration.CourseId == second.CourseId)
            .ToList();

        var secondByStudent = secondRegistrations
            .GroupBy(registration => registration.StudentId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var normal = 0;
        var carry = 0;

        foreach (var firstRegistration in firstRegistrations)
        {
            if (!secondByStudent.TryGetValue(
                    firstRegistration.StudentId,
                    out var matching))
            {
                continue;
            }

            if (firstRegistration.RegistrationType == "Carry" ||
                matching.Any(registration =>
                    registration.RegistrationType == "Carry"))
            {
                carry++;
            }
            else
            {
                normal++;
            }
        }

        return new StudentConflictSummary(normal, carry);
    }

    public static bool Overlaps(TimetableEntry first, TimetableEntry second) =>
        first.DayOfWeek.Equals(
            second.DayOfWeek,
            StringComparison.OrdinalIgnoreCase) &&
        first.StartMinutes < second.EndMinutes &&
        second.StartMinutes < first.EndMinutes;
}

public sealed record TimetableSlot(string DayOfWeek, int StartMinutes, int EndMinutes)
{
    public string Value => $"{DayOfWeek}|{StartMinutes}|{EndMinutes}";

    public string Label =>
        $"{DayOfWeek} {FormatTime(StartMinutes)}–{FormatTime(EndMinutes)}";

    private static string FormatTime(int minutes) =>
        new TimeOnly(minutes / 60, minutes % 60).ToString("HH:mm");
}

public sealed record TimetableValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

public sealed record TimetableIssue(
    string Category,
    string Message,
    IReadOnlyList<int> TimetableEntryIds);

public sealed record StudentConflictSummary(
    int NormalConflictCount,
    int CarryConflictCount)
{
    public int TotalCount => NormalConflictCount + CarryConflictCount;
}