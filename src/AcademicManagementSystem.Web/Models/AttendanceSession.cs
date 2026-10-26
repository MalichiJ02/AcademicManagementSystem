namespace AcademicManagementSystem.Web.Models;

public sealed class AttendanceSession
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public DateTime SessionDate { get; set; }
    public string Topic { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Course Course { get; set; } = null!;
    public ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();
}
