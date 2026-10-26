using AcademicManagementSystem.Web.Models;

namespace AcademicManagementSystem.Web.Services;

public sealed class TimetableService
{
    public static readonly IReadOnlyList<TimetableSlot> StandardSlots =
    [
        new("Monday", 540, 660),
        new("Monday", 690, 810),
        new("Monday", 840, 960),
        new("Monday", 1020, 1140),

        new("Tuesday", 540, 660),
        new("Tuesday", 690, 810),
        new("Tuesday", 840, 960),
        new("Tuesday", 1020, 1140),

        new("Wednesday", 540, 660),
        new("Wednesday", 690, 810),
        new("Wednesday", 840, 960),
        new("Wednesday", 1020, 1140),

        new("Thursday", 540, 660),
        new("Thursday", 690, 810),
        new("Thursday", 840, 960),
        new("Thursday", 1020, 1140),

        new("Friday", 540, 660),
        new("Friday", 690, 810),
        new("Friday", 1020, 1140)
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

        if (!candidate.Lecturer.IsAvailableOn(candidate.DayOfWeek))
        {
            errors.Add($"{candidate.Lecturer.FullName} is not available on {candidate.DayOfWeek}.");
        }

        foreach (var entry in existingEntries.Where(entry =>
                     !ReferenceEquals(entry, candidate) &&
                     (candidate.Id == 0 || entry.Id != candidate.Id)))
        {
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

            if (candidate.LecturerId == entry.LecturerId)
            {
                errors.Add(
                    $"Lecturer conflict: {candidate.Lecturer.FullName} is already assigned to {entry.Course.Code} " +
                    $"at {entry.ScheduleLabel}.");
            }

            var conflict = AnalyseStudentConflicts(candidate, entry, registrations);

            // Normal student conflicts are never allowed.
            if (conflict.NormalConflictCount > 0)
            {
                errors.Add(
                    $"Normal student timetable conflict: {conflict.NormalConflictCount} registered student(s) " +
                    $"take both {candidate.Course.Code} and {entry.Course.Code}.");
            }

            // Carry-course conflicts are not a hard error. They are shown
            // later as Coordinator warnings and minimised by the generator.
        }

        return new TimetableValidationResult(errors.Count == 0, errors.Distinct().ToList());
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

            if (!entry.Lecturer.IsAvailableOn(entry.DayOfWeek))
            {
                issues.Add(new TimetableIssue(
                    "Availability",
                    $"{entry.Lecturer.FullName} is unavailable on {entry.DayOfWeek} for {entry.Course.Code}.",
                    [entry.Id]));
            }
        }

        for (var firstIndex = 0; firstIndex < entries.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < entries.Count; secondIndex++)
            {
                var first = entries.ElementAt(firstIndex);
                var second = entries.ElementAt(secondIndex);

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

                if (first.LecturerId == second.LecturerId)
                {
                    issues.Add(new TimetableIssue(
                        "Lecturer conflict",
                        $"{first.Lecturer.FullName} is double-booked for {first.Course.Code} and " +
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
        TimetableEntry? bestCandidate = null;
        var bestScore = int.MaxValue;

        foreach (var slot in StandardSlots.Where(slot => IsSlotCompatibleWithCourse(course, slot)))
        {
            foreach (var lecturer in lecturers.Where(lecturer =>
                         lecturer.IsActive &&
                         lecturer.IsAvailableOn(slot.DayOfWeek)))
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

                    if (!PassesHardConstraints(candidate, existingEntries, registrations))
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

                        // A perfect candidate has no carry-course clash.
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

    public static bool IsSlotCompatibleWithCourse(Course course, TimetableSlot slot)
    {
        if (course.StudyMode is null)
        {
            return true;
        }

        var isEveningCourse = course.StudyMode.Name.Equals(
            "Evening",
            StringComparison.OrdinalIgnoreCase);

        var isEveningSlot = slot.StartMinutes >= 1020;

        return isEveningCourse
            ? isEveningSlot
            : !isEveningSlot;
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
            !candidate.Lecturer.IsAvailableOn(candidate.DayOfWeek))
        {
            return false;
        }

        foreach (var entry in existingEntries.Where(entry =>
                     !ReferenceEquals(entry, candidate) &&
                     (candidate.Id == 0 || entry.Id != candidate.Id) &&
                     Overlaps(candidate, entry)))
        {
            if (entry.RoomId == candidate.RoomId ||
                entry.LecturerId == candidate.LecturerId)
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

        foreach (var entry in existingEntries.Where(entry => Overlaps(candidate, entry)))
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
            if (!secondByStudent.TryGetValue(firstRegistration.StudentId, out var matching))
            {
                continue;
            }

            if (firstRegistration.RegistrationType == "Carry" ||
                matching.Any(registration => registration.RegistrationType == "Carry"))
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
        first.DayOfWeek.Equals(second.DayOfWeek, StringComparison.OrdinalIgnoreCase) &&
        first.StartMinutes < second.EndMinutes &&
        second.StartMinutes < first.EndMinutes;
}

public sealed record TimetableSlot(string DayOfWeek, int StartMinutes, int EndMinutes)
{
    public string Value => $"{DayOfWeek}|{StartMinutes}|{EndMinutes}";

    public string Label => $"{DayOfWeek} {FormatTime(StartMinutes)}–{FormatTime(EndMinutes)}";

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