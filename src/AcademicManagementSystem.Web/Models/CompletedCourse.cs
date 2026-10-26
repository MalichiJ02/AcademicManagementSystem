namespace AcademicManagementSystem.Web.Models;

public sealed class CompletedCourse
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int? CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public bool IsPassed { get; set; }
    public decimal? Mark { get; set; }
    public string? Grade { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;

    public Student Student { get; set; } = null!;
    public Course? Course { get; set; }
}
