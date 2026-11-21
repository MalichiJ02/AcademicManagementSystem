namespace AcademicManagementSystem.Web.Models;

public sealed class Course
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public int? ProgrammeId { get; set; }
    public Programme? Programme { get; set; }

    public int? StudyModeId { get; set; }
    public StudyMode? StudyMode { get; set; }

    public int CreditUnits { get; set; }
    public int YearLevel { get; set; }

    public string SemesterOffered { get; set; } = "Semester 2";

    // The number of separate teaching sessions this course needs every week.
    // Standard courses use two sessions by default.
    public int SessionsPerWeek { get; set; } = 2;

    public string? PrerequisiteCourseCode { get; set; }
    public string ScheduleKey { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public int ExpectedEnrollment { get; set; }
    public string CourseType { get; set; } = "Core";
    public bool IsActive { get; set; } = true;

    public ICollection<CourseRegistration> Registrations { get; set; } =
        new List<CourseRegistration>();

    public ICollection<TimetableEntry> TimetableEntries { get; set; } =
        new List<TimetableEntry>();
    public ICollection<CourseLecturerAssignment> LecturerAssignments { get; set; } =
    new List<CourseLecturerAssignment>();    

    public ICollection<AttendanceSession> AttendanceSessions { get; set; } =
        new List<AttendanceSession>();

    public ICollection<CoursePrerequisite> Prerequisites { get; set; } =
        new List<CoursePrerequisite>();

    public ICollection<CoursePrerequisite> RequiredFor { get; set; } =
        new List<CoursePrerequisite>();
}