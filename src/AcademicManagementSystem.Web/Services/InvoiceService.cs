using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Services;

public sealed class InvoiceService(AcademicManagementDbContext database)
{
    public async Task<InvoiceGenerationResult> GenerateCurrentInvoiceAsync(
        int studentId,
        AcademicSettings settings)
    {
        var student = await database.Students
            .Include(item => item.ProgrammeRecord)
            .Include(item => item.StudyModeRecord)
            .SingleOrDefaultAsync(item => item.Id == studentId);

        if (student is null)
        {
            return InvoiceGenerationResult.Failure("The student record no longer exists.");
        }

        var existing = await database.Invoices
            .SingleOrDefaultAsync(invoice =>
                invoice.StudentId == studentId &&
                invoice.AcademicYear == settings.AcademicYear &&
                invoice.Semester == settings.Semester);
        if (existing is not null)
        {
            return InvoiceGenerationResult.Success(existing, false);
        }

        if (student.ProgrammeRecord is null || student.StudyModeRecord is null)
        {
            return InvoiceGenerationResult.Failure(
                "Your programme or study mode has not been configured. Please contact the administrator.");
        }

        var lateFee = settings.IsLateRegistrationOpen ? settings.LateRegistrationFee : 0m;
        var total = student.ProgrammeRecord.TuitionAmount +
                    student.StudyModeRecord.AdditionalFee +
                    lateFee;

        if (total <= 0)
        {
            return InvoiceGenerationResult.Failure(
                "The programme and study-mode fee configuration must be completed before an invoice can be generated.");
        }

        var invoice = new Invoice
        {
            StudentId = student.Id,
            InvoiceNumber = BuildInvoiceNumber(settings, student),
            AcademicYear = settings.AcademicYear,
            Semester = settings.Semester,
            ProgrammeName = student.ProgrammeRecord.Name,
            StudyModeName = student.StudyModeRecord.Name,
            TotalAmount = total,
            AmountPaid = 0m,
            LateRegistrationFee = lateFee,
            GeneratedAt = DateTime.UtcNow,
            LastSyncedAt = DateTime.UtcNow,
            PaymentSource = "Administrator-recorded prototype payment",
            PaymentAccount = $"{student.StudyModeRecord.PaymentAccountName} · {student.StudyModeRecord.PaymentAccountReference}",
            Status = "Open"
        };

        database.Invoices.Add(invoice);
        await database.SaveChangesAsync();
        return InvoiceGenerationResult.Success(invoice, true);
    }

    public async Task<PaymentRecordResult> RecordPaymentAsync(
        int invoiceId,
        decimal amount,
        string reference,
        string? note,
        string? recordedByUserId)
    {
        var invoice = await database.Invoices
            .Include(item => item.Payments)
            .SingleOrDefaultAsync(item => item.Id == invoiceId);

        if (invoice is null)
        {
            return PaymentRecordResult.Failure("The invoice was not found.");
        }

        if (amount <= 0m)
        {
            return PaymentRecordResult.Failure("Payment amount must be greater than zero.");
        }

        if (amount > invoice.Balance)
        {
            return PaymentRecordResult.Failure(
                $"Payment cannot exceed the outstanding balance of ZMW {invoice.Balance:N2}.");
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            return PaymentRecordResult.Failure("Enter a payment reference for traceability.");
        }

        var payment = new PaymentRecord
        {
            InvoiceId = invoice.Id,
            Amount = amount,
            PaidAt = DateTime.UtcNow,
            Reference = reference.Trim(),
            PaymentAccount = invoice.PaymentAccount,
            RecordedByUserId = recordedByUserId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };

        database.PaymentRecords.Add(payment);
        invoice.AmountPaid = Math.Round(invoice.AmountPaid + amount, 2);
        invoice.LastSyncedAt = DateTime.UtcNow;
        invoice.Status = invoice.Balance <= 0m ? "Paid" : "Partially paid";
        await database.SaveChangesAsync();
        return PaymentRecordResult.Success(payment);
    }

    private static string BuildInvoiceNumber(AcademicSettings settings, Student student)
    {
        var semesterPart = string.Concat(settings.Semester
            .Where(char.IsLetterOrDigit)
            .Take(12))
            .ToUpperInvariant();
        var studentPart = string.Concat(student.StudentNumber
            .Where(char.IsLetterOrDigit)
            .Take(20))
            .ToUpperInvariant();
        return $"INV-{settings.AcademicYear}-{semesterPart}-{studentPart}";
    }
}

public sealed record InvoiceGenerationResult(
    bool Succeeded,
    string? Error,
    Invoice? Invoice,
    bool Created)
{
    public static InvoiceGenerationResult Success(Invoice invoice, bool created) =>
        new(true, null, invoice, created);

    public static InvoiceGenerationResult Failure(string error) =>
        new(false, error, null, false);
}

public sealed record PaymentRecordResult(
    bool Succeeded,
    string? Error,
    PaymentRecord? Payment)
{
    public static PaymentRecordResult Success(PaymentRecord payment) =>
        new(true, null, payment);

    public static PaymentRecordResult Failure(string error) =>
        new(false, error, null);
}
