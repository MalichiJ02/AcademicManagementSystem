namespace AcademicManagementSystem.Web.Models;

public sealed class Lecturer
{
    public int Id { get; set; }
    public string StaffNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Specialisation { get; set; } = string.Empty;
    public string AvailableDays { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<TimetableEntry> TimetableEntries { get; set; } = new List<TimetableEntry>();

    public bool IsAvailableOn(string day) =>
        AvailableDays.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(day, StringComparer.OrdinalIgnoreCase);
public ICollection<CourseLecturerAssignment> CourseAssignments { get; set; }
    = new List<CourseLecturerAssignment>();
}
