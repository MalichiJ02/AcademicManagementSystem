namespace AcademicManagementSystem.Web.Models;

public sealed class CourseLecturerAssignment
{
    public int Id { get; set; }

    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public int LecturerId { get; set; }
    public Lecturer Lecturer { get; set; } = null!;

    public bool IsPrimary { get; set; } = true;
    public bool IsActive { get; set; } = true;
}