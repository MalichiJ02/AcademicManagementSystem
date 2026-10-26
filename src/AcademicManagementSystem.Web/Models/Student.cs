namespace AcademicManagementSystem.Web.Models;

public sealed class Student
{
    public int Id { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string? IdentityUserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    // Kept as a snapshot for reports and backwards compatibility. The linked
    // ProgrammeRecord is the administrator-managed source of truth.
    public string Programme { get; set; } = string.Empty;
    public int? ProgrammeId { get; set; }
    public Programme? ProgrammeRecord { get; set; }
    public string StudyMode { get; set; } = "Full-time";
    public int? StudyModeId { get; set; }
    public StudyMode? StudyModeRecord { get; set; }
    public int YearOfStudy { get; set; }
    public bool IsAcademicallyEligible { get; set; }
    public string? AcademicHoldReason { get; set; }
    public string ProgressionStatus { get; set; } = "Normal";
    public string? ProgressionReason { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<CourseRegistration> Registrations { get; set; } = new List<CourseRegistration>();
    public ICollection<CompletedCourse> CompletedCourses { get; set; } = new List<CompletedCourse>();
    public ICollection<StudentResult> Results { get; set; } = new List<StudentResult>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    public string FullName => $"{FirstName} {LastName}";
}
