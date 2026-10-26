using System.ComponentModel.DataAnnotations.Schema;

namespace AcademicManagementSystem.Web.Models;

public sealed class TimetableEntry
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int LecturerId { get; set; }
    public int RoomId { get; set; }
    public string AcademicYear { get; set; } = "2026";
    public string Semester { get; set; } = "Semester 2";
    public string DayOfWeek { get; set; } = string.Empty;
    public int StartMinutes { get; set; }
    public int EndMinutes { get; set; }
    public string Status { get; set; } = "Published";

    public Course Course { get; set; } = null!;
    public Lecturer Lecturer { get; set; } = null!;
    public Room Room { get; set; } = null!;

    [NotMapped]
    public string TimeRange => $"{FormatTime(StartMinutes)}–{FormatTime(EndMinutes)}";

    [NotMapped]
    public string ScheduleLabel => $"{DayOfWeek} {TimeRange}";

    private static string FormatTime(int minutes) =>
        new TimeOnly(minutes / 60, minutes % 60).ToString("HH:mm");
}
