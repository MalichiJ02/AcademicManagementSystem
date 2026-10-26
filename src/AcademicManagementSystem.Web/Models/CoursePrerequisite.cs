namespace AcademicManagementSystem.Web.Models;

/// <summary>Represents one required passed course before a course can be selected.</summary>
public sealed class CoursePrerequisite
{
    public int CourseId { get; set; }
    public int PrerequisiteCourseId { get; set; }

    public Course Course { get; set; } = null!;
    public Course PrerequisiteCourse { get; set; } = null!;
}
