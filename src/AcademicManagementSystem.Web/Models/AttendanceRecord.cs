using System.ComponentModel.DataAnnotations.Schema;

namespace AcademicManagementSystem.Web.Models;

public sealed class AttendanceRecord
{
    public int Id { get; set; }
    public int AttendanceSessionId { get; set; }
    public int StudentId { get; set; }
    public string Status { get; set; } = "Present";
    public DateTime RecordedAt { get; set; }

    public AttendanceSession AttendanceSession { get; set; } = null!;
    public Student Student { get; set; } = null!;

    [NotMapped]
    public bool IsAttending =>
        Status.Equals("Present", StringComparison.OrdinalIgnoreCase) ||
        Status.Equals("Late", StringComparison.OrdinalIgnoreCase);
}
