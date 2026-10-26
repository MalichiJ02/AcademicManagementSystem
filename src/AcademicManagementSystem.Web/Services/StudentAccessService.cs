using AcademicManagementSystem.Web.Models;

namespace AcademicManagementSystem.Web.Services;

public sealed class StudentAccessService
{
    public FinancialClearance EvaluateFinancialClearance(Invoice invoice) =>
        EvaluateFinancialClearance(invoice, new AcademicSettings());

    public FinancialClearance EvaluateFinancialClearance(
        Invoice invoice,
        AcademicSettings settings) =>
        new(
            invoice.PaymentPercentage,
            invoice.Balance,
            invoice.PaymentPercentage >= settings.RegistrationPaymentPercentage,
            invoice.PaymentPercentage >= settings.ExamSlipPaymentPercentage,
            invoice.PaymentPercentage >= settings.ResultsPaymentPercentage);

    public AccessDecision EvaluateRegistration(Student student, Invoice invoice)
        => EvaluateRegistration(student, invoice, new AcademicSettings());

    public AccessDecision EvaluateRegistration(
        Student student,
        Invoice invoice,
        AcademicSettings settings)
    {
        var reasons = new List<string>();

        if (!settings.IsRegistrationOpen)
        {
            reasons.Add("Course registration is currently closed by the administrator.");
        }

        if (invoice.PaymentPercentage < settings.RegistrationPaymentPercentage)
        {
            reasons.Add(
                $"Registration is locked until at least {settings.RegistrationPaymentPercentage:N0}% " +
                $"of the invoice is paid. Current payment: {invoice.PaymentPercentage:N0}%.");
        }

        if (!student.IsAcademicallyEligible)
        {
            reasons.Add(student.AcademicHoldReason ?? "Academic eligibility requirements have not been met.");
        }

        return AccessDecision.FromReasons(reasons);
    }

    public AccessDecision EvaluateExamSlip(Invoice invoice, bool hasConfirmedRegistration)
        => EvaluateExamSlip(invoice, hasConfirmedRegistration, new AcademicSettings());

    public AccessDecision EvaluateExamSlip(
        Invoice invoice,
        bool hasConfirmedRegistration,
        AcademicSettings settings)
    {
        var reasons = new List<string>();

        if (invoice.PaymentPercentage < settings.ExamSlipPaymentPercentage)
        {
            reasons.Add(
                $"An exam slip requires at least {settings.ExamSlipPaymentPercentage:N0}% payment. " +
                $"Current payment: {invoice.PaymentPercentage:N0}%.");
        }

        if (!hasConfirmedRegistration)
        {
            reasons.Add("An exam slip requires a confirmed semester registration.");
        }

        return AccessDecision.FromReasons(reasons);
    }

    public AccessDecision EvaluateResults(Invoice invoice, bool hasPublishedResults)
        => EvaluateResults(invoice, hasPublishedResults, new AcademicSettings());

    public AccessDecision EvaluateResults(
        Invoice invoice,
        bool hasPublishedResults,
        AcademicSettings settings)
    {
        var reasons = new List<string>();

        if (invoice.PaymentPercentage < settings.ResultsPaymentPercentage)
        {
            reasons.Add(
                $"Results require {settings.ResultsPaymentPercentage:N0}% payment. " +
                $"Current payment: {invoice.PaymentPercentage:N0}%.");
        }

        if (!hasPublishedResults)
        {
            reasons.Add("Results are not yet published for this student.");
        }

        return AccessDecision.FromReasons(reasons);
    }
}

public sealed record FinancialClearance(
    decimal PaymentPercentage,
    decimal Balance,
    bool RegistrationFinanciallyCleared,
    bool ExamSlipFinanciallyCleared,
    bool ResultsFinanciallyCleared);

public sealed record AccessDecision(bool Allowed, IReadOnlyList<string> Reasons)
{
    public static AccessDecision FromReasons(IReadOnlyList<string> reasons) =>
        new(reasons.Count == 0, reasons);
}
