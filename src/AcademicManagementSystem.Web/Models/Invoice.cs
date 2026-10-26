using System.ComponentModel.DataAnnotations.Schema;

namespace AcademicManagementSystem.Web.Models;

public sealed class Invoice
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public string ProgrammeName { get; set; } = string.Empty;
    public string StudyModeName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal LateRegistrationFee { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public string PaymentSource { get; set; } = "Administrator-recorded prototype payment";
    public string PaymentAccount { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";

    public Student Student { get; set; } = null!;
    public ICollection<PaymentRecord> Payments { get; set; } = new List<PaymentRecord>();

    [NotMapped]
    public decimal Balance => Math.Max(0m, TotalAmount - AmountPaid);

    [NotMapped]
    public decimal PaymentPercentage => TotalAmount == 0m
        ? 0m
        : Math.Round((AmountPaid / TotalAmount) * 100m, 2);
}
