namespace AcademicManagementSystem.Web.Models;

public sealed class StudyMode
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MaximumCourses { get; set; } = 4;
    public decimal AdditionalFee { get; set; }
    public string PaymentAccountName { get; set; } = string.Empty;
    public string PaymentAccountReference { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
