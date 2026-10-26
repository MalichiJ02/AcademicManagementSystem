using AcademicManagementSystem.Web.Models;

namespace AcademicManagementSystem.Web.Services;

public sealed class RegistrationService(AcademicProgressionService progressionService)
{
    // Preserves the original prototype contract for any legacy page while new
    // student pages pass the administrator's actual settings explicitly.
    public RegistrationValidationResult Validate(
        Student student,
        IReadOnlyCollection<Course> selectedCourses,
        IReadOnlyCollection<CourseRegistration> existingRegistrations,
        IReadOnlyCollection<TimetableEntry> timetableEntries) =>
        Validate(
            student,
            new AcademicSettings(),
            selectedCourses,
            existingRegistrations,
            timetableEntries);

    public RegistrationValidationResult Validate(
        Student student,
        AcademicSettings settings,
        IReadOnlyCollection<Course> selectedCourses,
        IReadOnlyCollection<CourseRegistration> existingRegistrations,
        IReadOnlyCollection<TimetableEntry> timetableEntries)
    {
        var errors = new List<string>();
        var passedCourseCodes = student.CompletedCourses
            .Where(course => course.IsPassed)
            .Select(course => course.CourseCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var failedCourseCodes = student.CompletedCourses
            .Where(course => !course.IsPassed)
            .Select(course => course.CourseCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var progression = progressionService.Assess(student, settings);
        var timetableByCourseId = timetableEntries
            .GroupBy(entry => entry.CourseId)
            .ToDictionary(group => group.Key, group => group.OrderBy(entry => entry.Id).First());

        var maximumCourses = settings.MaximumTotalCourses;
        if (student.StudyModeRecord is { MaximumCourses: > 0 })
        {
            maximumCourses = Math.Min(maximumCourses, student.StudyModeRecord.MaximumCourses);
        }

        foreach (var course in selectedCourses)
        {
            if (!course.IsActive)
            {
                errors.Add($"{course.Code} is not active.");
            }

            if (course.ProgrammeId.HasValue &&
                student.ProgrammeId.HasValue &&
                course.ProgrammeId != student.ProgrammeId)
            {
                errors.Add($"{course.Code} does not belong to the student's programme.");
            }

            if (course.StudyModeId.HasValue &&
                student.StudyModeId.HasValue &&
                course.StudyModeId != student.StudyModeId)
            {
                errors.Add($"{course.Code} is not offered for the student's study mode.");
            }

            if (!IsOfferedThisSemester(course, settings.Semester))
            {
                errors.Add($"{course.Code} is not offered in {settings.Semester}.");
            }

            if (course.YearLevel > student.YearOfStudy)
            {
                errors.Add($"{course.Code} is not available for the student's current year of study.");
            }

            var isCarry = failedCourseCodes.Contains(course.Code);
            if (course.YearLevel < student.YearOfStudy && !isCarry)
            {
                errors.Add($"{course.Code} can only be selected as an outstanding carry course.");
            }

            if (passedCourseCodes.Contains(course.Code))
            {
                errors.Add($"{course.Code} has already been passed.");
            }

            var prerequisiteCodes = course.Prerequisites
                .Select(link => link.PrerequisiteCourse.Code)
                .Append(course.PrerequisiteCourseCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var prerequisiteCode in prerequisiteCodes)
            {
                if (!passedCourseCodes.Contains(prerequisiteCode))
                {
                    errors.Add($"{course.Code} requires a passed {prerequisiteCode} prerequisite.");
                }
            }

            if (!timetableByCourseId.ContainsKey(course.Id))
            {
                errors.Add($"{course.Code} has no published timetable entry yet.");
            }

            if (progression.IsRestricted && !isCarry)
            {
                errors.Add(
                    $"{course.Code} cannot be selected while the student is restricted to failed carry courses.");
            }
        }

        var duplicates = selectedCourses
            .Where(course => existingRegistrations.Any(registration => registration.CourseId == course.Id))
            .Select(course => course.Code)
            .ToList();
        if (duplicates.Count > 0)
        {
            errors.Add($"Already registered: {string.Join(", ", duplicates)}.");
        }

        var confirmedExisting = existingRegistrations
            .Where(registration => registration.Status == "Confirmed")
            .ToList();
        var totalCourseCount = confirmedExisting.Count + selectedCourses.Count;
        if (totalCourseCount > maximumCourses)
        {
            errors.Add(
                $"{student.StudyModeRecord?.Name ?? "The selected study mode"} allows a maximum of " +
                $"{maximumCourses} course(s) this semester. This selection would total {totalCourseCount}.");
        }

        var carryCourseCount = confirmedExisting
            .Count(registration => registration.RegistrationType == "Carry") +
            selectedCourses.Count(course => failedCourseCodes.Contains(course.Code));
        if (!progression.IsRestricted && carryCourseCount > settings.MaximumCarryCourses)
        {
            errors.Add(
                $"Normal registration allows up to {settings.MaximumCarryCourses} carry course(s). " +
                $"This selection would include {carryCourseCount}.");
        }

        var existingCreditUnits = confirmedExisting.Sum(registration => registration.Course.CreditUnits);
        var requestedCreditUnits = selectedCourses.Sum(course => course.CreditUnits);

        var scheduledCourses = selectedCourses
            .Where(course => timetableByCourseId.ContainsKey(course.Id))
            .Select(course => new ScheduledCourse(course, timetableByCourseId[course.Id]))
            .ToList();
        var existingScheduledCourses = confirmedExisting
            .Where(registration => timetableByCourseId.ContainsKey(registration.CourseId))
            .Select(registration => new ScheduledCourse(
                registration.Course,
                timetableByCourseId[registration.CourseId]))
            .ToList();

        foreach (var pair in AllPairs(scheduledCourses))
        {
            if (Overlaps(pair.First.Entry, pair.Second.Entry))
            {
                errors.Add(
                    $"Selected-course timetable clash: {pair.First.Entry.ScheduleLabel} " +
                    $"({pair.First.Course.Code} and {pair.Second.Course.Code}).");
            }
        }

        foreach (var selected in scheduledCourses)
        {
            var clashingExisting = existingScheduledCourses
                .Where(existing => Overlaps(selected.Entry, existing.Entry))
                .Select(existing => existing.Course.Code)
                .ToList();

            if (clashingExisting.Count > 0)
            {
                errors.Add(
                    $"Clash with existing registration: {selected.Course.Code} at {selected.Entry.ScheduleLabel} " +
                    $"conflicts with {string.Join(", ", clashingExisting)}.");
            }
        }

        var registrationTypes = selectedCourses.ToDictionary(
            course => course.Id,
            course => failedCourseCodes.Contains(course.Code) ? "Carry" : "Normal");

        return new RegistrationValidationResult(
            errors.Count == 0,
            errors.Distinct().ToList(),
            registrationTypes,
            existingCreditUnits + requestedCreditUnits,
            totalCourseCount,
            carryCourseCount,
            progression);
    }

    private static bool IsOfferedThisSemester(Course course, string semester) =>
        course.SemesterOffered.Equals("Both", StringComparison.OrdinalIgnoreCase) ||
        course.SemesterOffered.Equals(semester, StringComparison.OrdinalIgnoreCase);

    private static bool Overlaps(TimetableEntry first, TimetableEntry second) =>
        first.DayOfWeek.Equals(second.DayOfWeek, StringComparison.OrdinalIgnoreCase) &&
        first.StartMinutes < second.EndMinutes &&
        second.StartMinutes < first.EndMinutes;

    private static IEnumerable<(ScheduledCourse First, ScheduledCourse Second)> AllPairs(
        IReadOnlyList<ScheduledCourse> courses)
    {
        for (var firstIndex = 0; firstIndex < courses.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < courses.Count; secondIndex++)
            {
                yield return (courses[firstIndex], courses[secondIndex]);
            }
        }
    }

    private sealed record ScheduledCourse(Course Course, TimetableEntry Entry);
}

public sealed record RegistrationValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<int, string> RegistrationTypes,
    int TotalCreditUnits,
    int TotalCourseCount,
    int CarryCourseCount,
    ProgressionAssessment Progression);
