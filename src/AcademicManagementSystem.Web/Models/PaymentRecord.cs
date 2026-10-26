namespace AcademicManagementSystem.Web.Models;

public sealed class PaymentRecord
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string PaymentAccount { get; set; } = string.Empty;
    public string? RecordedByUserId { get; set; }
    public string? Note { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
