namespace AcademicManagementSystem.Web.Models;

public sealed class CourseRegistration
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public string Status { get; set; } = "Confirmed";
    public string RegistrationType { get; set; } = "Normal";

    public Student Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
}
