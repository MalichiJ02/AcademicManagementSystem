using AcademicManagementSystem.Web.Models;

namespace AcademicManagementSystem.Web.Services;

public sealed class AcademicRiskService(AttendanceService attendanceService)
{
    public AcademicRiskAssessment Assess(Student student)
    {
        var attendance = attendanceService.Summarise(student.AttendanceRecords);
        var resultMarks = student.Results
            .Select(result => result.Mark)
            .ToList();
        decimal? averageMark = resultMarks.Count == 0
            ? null
            : Math.Round(resultMarks.Average(), 1);

        var reasons = new List<string>();
        var level = "Low";

        if (!student.IsAcademicallyEligible)
        {
            level = "High";
            reasons.Add(student.AcademicHoldReason ?? "Academic eligibility hold is active.");
        }

        if (attendance.AttendancePercentage is < 60m)
        {
            level = "High";
            reasons.Add($"Attendance is {attendance.AttendancePercentage:N1}%, below the 60% high-risk threshold.");
        }
        else if (attendance.AttendancePercentage is < 75m && level != "High")
        {
            level = "Moderate";
            reasons.Add($"Attendance is {attendance.AttendancePercentage:N1}%, below the 75% monitoring threshold.");
        }

        if (averageMark is < 50m)
        {
            level = "High";
            reasons.Add($"Average result is {averageMark:N1}%, below the 50% high-risk threshold.");
        }
        else if (averageMark is < 65m && level == "Low")
        {
            level = "Moderate";
            reasons.Add($"Average result is {averageMark:N1}%; provide academic support early.");
        }

        if (reasons.Count == 0)
        {
            reasons.Add(
                attendance.TotalSessions == 0 && averageMark is null
                    ? "No attendance or assessment data is available yet."
                    : "Current attendance and assessment indicators are within the configured monitoring thresholds.");
        }

        return new AcademicRiskAssessment(
            student.Id,
            student.FullName,
            student.StudentNumber,
            level,
            attendance.AttendancePercentage,
            averageMark,
            reasons);
    }
}

public sealed record AcademicRiskAssessment(
    int StudentId,
    string StudentName,
    string StudentNumber,
    string Level,
    decimal? AttendancePercentage,
    decimal? AverageMark,
    IReadOnlyList<string> Reasons);
