namespace AcademicManagementSystem.Web.Models;

public sealed class StudentResult
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int? CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public decimal Mark { get; set; }
    public string Grade { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = "2026";
    public string Semester { get; set; } = "Semester 2";
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }

    public Student Student { get; set; } = null!;
    public Course? Course { get; set; }
}
