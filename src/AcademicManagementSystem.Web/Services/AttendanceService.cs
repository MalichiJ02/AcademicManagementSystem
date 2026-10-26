using AcademicManagementSystem.Web.Models;

namespace AcademicManagementSystem.Web.Services;

public sealed class AttendanceService
{
    public AttendanceSummary Summarise(IEnumerable<AttendanceRecord> records)
    {
        var recordList = records.ToList();
        var attended = recordList.Count(record => record.IsAttending);

        return new AttendanceSummary(
            recordList.Count,
            attended,
            recordList.Count - attended,
            recordList.Count == 0
                ? null
                : Math.Round((decimal)attended / recordList.Count * 100m, 1));
    }
}

public sealed record AttendanceSummary(
    int TotalSessions,
    int AttendedSessions,
    int MissedSessions,
    decimal? AttendancePercentage);
