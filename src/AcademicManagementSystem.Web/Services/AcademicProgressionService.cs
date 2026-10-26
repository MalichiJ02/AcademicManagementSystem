using AcademicManagementSystem.Web.Models;

namespace AcademicManagementSystem.Web.Services;

public sealed class AcademicProgressionService
{
    public ProgressionAssessment Assess(Student student, AcademicSettings settings)
    {
        var outstandingCarries = student.CompletedCourses
            .Where(course => !course.IsPassed)
            .Select(course => course.CourseCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code)
            .ToList();
        var outstandingCarrySet = outstandingCarries
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var failureGroups = student.Results
            .Where(result => result.Mark < 50m)
            .GroupBy(result => new { result.AcademicYear, result.Semester })
            .Select(group => new
            {
                group.Key.AcademicYear,
                group.Key.Semester,
                FailedCourseCodes = group
                    .Select(result => result.CourseCode)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .OrderByDescending(group => group.AcademicYear)
            .ThenByDescending(group => group.Semester)
            .ToList();

        // A restriction applies while failed courses from a qualifying semester
        // remain outstanding. If all of those courses are later passed, the
        // student can progress normally again instead of being restricted forever.
        var restrictionGroup = failureGroups.FirstOrDefault(group =>
            group.FailedCourseCodes.Count > settings.FailedCoursesForRestriction &&
            group.FailedCourseCodes.Any(outstandingCarrySet.Contains));

        var restricted = restrictionGroup is not null;
        var restrictedOutstandingCourses = restrictionGroup?.FailedCourseCodes
            .Where(outstandingCarrySet.Contains)
            .ToList() ?? [];
        var reason = restricted
            ? $"More than {settings.FailedCoursesForRestriction} courses were failed in " +
              $"{restrictionGroup!.Semester}, {restrictionGroup.AcademicYear}. " +
              "Only outstanding failed courses may be selected until progression is regularised."
            : outstandingCarries.Count > settings.MaximumCarryCourses
                ? $"There are {outstandingCarries.Count} outstanding carry courses. " +
                  "Coordinator review is required before normal progression."
                : null;

        return new ProgressionAssessment(
            restricted,
            reason,
            outstandingCarries,
            restrictedOutstandingCourses);
    }

    public void Apply(Student student, AcademicSettings settings)
    {
        var assessment = Assess(student, settings);
        student.ProgressionStatus = assessment.IsRestricted ? "Restricted" : "Normal";
        student.ProgressionReason = assessment.Reason;
    }
}

public sealed record ProgressionAssessment(
    bool IsRestricted,
    string? Reason,
    IReadOnlyList<string> OutstandingCarryCourseCodes,
    IReadOnlyList<string> RestrictedFailedCourseCodes);
