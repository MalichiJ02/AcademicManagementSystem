namespace AcademicManagementSystem.Web.Models;

public sealed class Programme
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal TuitionAmount { get; set; }

    public int? SchoolId { get; set; }

    public School? School { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Student> Students { get; set; } = new List<Student>();

    public ICollection<Course> Courses { get; set; } = new List<Course>();
}