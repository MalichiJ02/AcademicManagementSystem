namespace AcademicManagementSystem.Web.Models;

/// <summary>
/// Administrator-controlled academic and financial rules for the prototype.
/// There is one active settings record.
/// </summary>
public sealed class AcademicSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string AcademicYear { get; set; } = "2026";
    public string Semester { get; set; } = "Semester 2";
    public bool IsRegistrationOpen { get; set; } = true;
    public bool IsLateRegistrationOpen { get; set; }
    public decimal RegistrationPaymentPercentage { get; set; } = 30m;
    public decimal ExamSlipPaymentPercentage { get; set; } = 80m;
    public decimal ResultsPaymentPercentage { get; set; } = 100m;
    public int MaximumTotalCourses { get; set; } = 4;
    public int MaximumCarryCourses { get; set; } = 2;
    public int FailedCoursesForRestriction { get; set; } = 2;
    public decimal LateRegistrationFee { get; set; } = 600m;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
