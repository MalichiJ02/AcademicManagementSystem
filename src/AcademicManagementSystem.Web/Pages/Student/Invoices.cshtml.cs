using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEntity = AcademicManagementSystem.Web.Models.Student;

namespace AcademicManagementSystem.Web.Pages.StudentPortal;

[Authorize(Roles = "Student")]
public class InvoicesModel(
    AcademicManagementDbContext database,
    CurrentStudentService currentStudentService,
    AcademicSettingsService settingsService,
    InvoiceService invoiceService) : PageModel
{
    public StudentEntity? Student { get; private set; }
    public AcademicSettings Settings { get; private set; } = new();
    public IReadOnlyList<Invoice> Invoices { get; private set; } = [];
    public Invoice? CurrentInvoice { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadPageAsync())
        {
            return RedirectToPage("/Account/AccessDenied");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostGenerateAsync()
    {
        var currentStudent = await currentStudentService.GetStudentAsync(User);
        if (currentStudent is null || !currentStudent.IsActive)
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        var settings = await settingsService.GetAsync();
        var result = await invoiceService.GenerateCurrentInvoiceAsync(currentStudent.Id, settings);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
        }
        else
        {
            TempData["Success"] = result.Created
                ? $"Invoice {result.Invoice!.InvoiceNumber} was generated from your programme and study mode."
                : $"Your current invoice {result.Invoice!.InvoiceNumber} already exists.";
        }

        return RedirectToPage();
    }

    private async Task<bool> LoadPageAsync()
    {
        var currentStudent = await currentStudentService.GetStudentAsync(User);
        if (currentStudent is null || !currentStudent.IsActive)
        {
            return false;
        }

        Settings = await settingsService.GetAsync();
        Student = await database.Students
            .Include(student => student.ProgrammeRecord)
            .Include(student => student.StudyModeRecord)
            .Include(student => student.Invoices)
                .ThenInclude(invoice => invoice.Payments)
            .AsSplitQuery()
            .AsNoTracking()
            .SingleAsync(student => student.Id == currentStudent.Id);
        Invoices = Student.Invoices
            .OrderByDescending(invoice => invoice.GeneratedAt)
            .ToList();
        CurrentInvoice = Invoices.SingleOrDefault(invoice =>
            invoice.AcademicYear == Settings.AcademicYear &&
            invoice.Semester == Settings.Semester);
        return true;
    }
}
