using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class FinanceModel(
    AcademicManagementDbContext database,
    InvoiceService invoiceService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public PaymentInput Payment { get; set; } = new();

    public IReadOnlyList<Invoice> Invoices { get; private set; } = [];

    public async Task OnGetAsync() => await LoadPageAsync();

    public async Task<IActionResult> OnPostRecordPaymentAsync()
    {
        if (Payment.InvoiceId <= 0 || Payment.Amount <= 0m || string.IsNullOrWhiteSpace(Payment.Reference))
        {
            ModelState.AddModelError(string.Empty, "Choose an invoice and enter a positive amount and payment reference.");
            await LoadPageAsync();
            return Page();
        }

        var currentUser = await userManager.GetUserAsync(User);
        var result = await invoiceService.RecordPaymentAsync(
            Payment.InvoiceId,
            Payment.Amount,
            Payment.Reference,
            Payment.Note,
            currentUser?.Id);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadPageAsync();
            return Page();
        }

        TempData["Success"] =
            $"Payment of ZMW {result.Payment!.Amount:N2} was recorded against the selected invoice.";
        return RedirectToPage();
    }

    private async Task LoadPageAsync()
    {
        Invoices = await database.Invoices
            .Include(invoice => invoice.Student)
            .Include(invoice => invoice.Payments)
            .AsSplitQuery()
            .AsNoTracking()
            .OrderByDescending(invoice => invoice.GeneratedAt)
            .ThenBy(invoice => invoice.Student.StudentNumber)
            .ToListAsync();
    }

    public sealed class PaymentInput
    {
        public int InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string? Note { get; set; }
    }
}
