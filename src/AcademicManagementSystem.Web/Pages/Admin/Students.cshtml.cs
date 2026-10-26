using System.ComponentModel.DataAnnotations;
using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudentEntity = AcademicManagementSystem.Web.Models.Student;

namespace AcademicManagementSystem.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class StudentsModel(
    AcademicManagementDbContext database,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public NewStudentInput NewStudent { get; set; } = new();

    [BindProperty]
    public UpdateStudentInput UpdateStudent { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? EditId { get; set; }

    public IReadOnlyList<Programme> Programmes { get; private set; } = [];
    public IReadOnlyList<StudyMode> StudyModes { get; private set; } = [];
    public IReadOnlyList<StudentEntity> Students { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await LoadPageAsync();

        if (EditId.HasValue)
        {
            var student = Students.SingleOrDefault(item => item.Id == EditId.Value);
            if (student is not null)
            {
                UpdateStudent = new UpdateStudentInput
                {
                    Id = student.Id,
                    StudentNumber = student.StudentNumber,
                    FirstName = student.FirstName,
                    LastName = student.LastName,
                    ProgrammeId = student.ProgrammeId ?? 0,
                    StudyModeId = student.StudyModeId ?? 0,
                    YearOfStudy = student.YearOfStudy,
                    IsAcademicallyEligible = student.IsAcademicallyEligible,
                    AcademicHoldReason = student.AcademicHoldReason,
                    IsActive = student.IsActive
                };
            }
        }
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ValidateNewStudent())
        {
            await LoadPageAsync();
            return Page();
        }

        var programme = await database.Programmes
            .SingleOrDefaultAsync(item => item.Id == NewStudent.ProgrammeId && item.IsActive);
        var studyMode = await database.StudyModes
            .SingleOrDefaultAsync(item => item.Id == NewStudent.StudyModeId && item.IsActive);
        if (programme is null || studyMode is null)
        {
            ModelState.AddModelError(string.Empty, "Choose an active programme and study mode.");
            await LoadPageAsync();
            return Page();
        }

        var numberExists = await database.Students
            .AnyAsync(item => item.StudentNumber == NewStudent.StudentNumber.Trim());
        var emailExists = await userManager.FindByEmailAsync(NewStudent.Email.Trim()) is not null;
        if (numberExists)
        {
            ModelState.AddModelError(string.Empty, "That student number is already in use.");
        }
        if (emailExists)
        {
            ModelState.AddModelError(string.Empty, "That email address is already in use.");
        }
        if (!ModelState.IsValid)
        {
            await LoadPageAsync();
            return Page();
        }

        var student = new StudentEntity
        {
            StudentNumber = NewStudent.StudentNumber.Trim().ToUpperInvariant(),
            FirstName = NewStudent.FirstName.Trim(),
            LastName = NewStudent.LastName.Trim(),
            Programme = programme.Name,
            ProgrammeId = programme.Id,
            StudyMode = studyMode.Name,
            StudyModeId = studyMode.Id,
            YearOfStudy = NewStudent.YearOfStudy,
            IsAcademicallyEligible = true,
            ProgressionStatus = "Normal",
            IsActive = true
        };
        database.Students.Add(student);
        await database.SaveChangesAsync();

        var account = new ApplicationUser
        {
            UserName = NewStudent.Email.Trim(),
            Email = NewStudent.Email.Trim(),
            FullName = student.FullName,
            StudentId = student.Id,
            EmailConfirmed = true
        };
        var accountResult = await userManager.CreateAsync(account, NewStudent.InitialPassword);
        if (!accountResult.Succeeded)
        {
            database.Students.Remove(student);
            await database.SaveChangesAsync();
            foreach (var error in accountResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await LoadPageAsync();
            return Page();
        }

        var roleResult = await userManager.AddToRoleAsync(account, "Student");
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(account);
            database.Students.Remove(student);
            await database.SaveChangesAsync();
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await LoadPageAsync();
            return Page();
        }

        student.IdentityUserId = account.Id;
        await database.SaveChangesAsync();

        TempData["Success"] =
            $"Student {student.FullName} and their secure student account were created.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        if (!ValidateUpdateStudent())
        {
            EditId = UpdateStudent.Id;
            await LoadPageAsync();
            return Page();
        }

        var student = await database.Students.SingleOrDefaultAsync(item => item.Id == UpdateStudent.Id);
        var programme = await database.Programmes
            .SingleOrDefaultAsync(item => item.Id == UpdateStudent.ProgrammeId && item.IsActive);
        var studyMode = await database.StudyModes
            .SingleOrDefaultAsync(item => item.Id == UpdateStudent.StudyModeId && item.IsActive);
        if (student is null || programme is null || studyMode is null)
        {
            TempData["Error"] = "The student, programme, or study mode is no longer available.";
            return RedirectToPage();
        }

        var numberUsedByAnotherStudent = await database.Students
            .AnyAsync(item => item.Id != student.Id &&
                              item.StudentNumber == UpdateStudent.StudentNumber.Trim());
        if (numberUsedByAnotherStudent)
        {
            ModelState.AddModelError(string.Empty, "That student number is already in use.");
            EditId = UpdateStudent.Id;
            await LoadPageAsync();
            return Page();
        }

        student.StudentNumber = UpdateStudent.StudentNumber.Trim().ToUpperInvariant();
        student.FirstName = UpdateStudent.FirstName.Trim();
        student.LastName = UpdateStudent.LastName.Trim();
        student.ProgrammeId = programme.Id;
        student.Programme = programme.Name;
        student.StudyModeId = studyMode.Id;
        student.StudyMode = studyMode.Name;
        student.YearOfStudy = UpdateStudent.YearOfStudy;
        student.IsAcademicallyEligible = UpdateStudent.IsAcademicallyEligible;
        student.AcademicHoldReason = UpdateStudent.IsAcademicallyEligible
            ? null
            : string.IsNullOrWhiteSpace(UpdateStudent.AcademicHoldReason)
                ? "Academic hold recorded by the administrator."
                : UpdateStudent.AcademicHoldReason.Trim();
        student.IsActive = UpdateStudent.IsActive;
        await database.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(student.IdentityUserId))
        {
            var account = await userManager.FindByIdAsync(student.IdentityUserId);
            if (account is not null)
            {
                account.FullName = student.FullName;
                account.StudentId = student.Id;
                account.IsActive = student.IsActive;
                await userManager.UpdateAsync(account);
            }
        }

        TempData["Success"] = $"Student information for {student.FullName} was updated.";
        return RedirectToPage();
    }

    private bool ValidateNewStudent()
    {
        if (string.IsNullOrWhiteSpace(NewStudent.StudentNumber) ||
            string.IsNullOrWhiteSpace(NewStudent.FirstName) ||
            string.IsNullOrWhiteSpace(NewStudent.LastName) ||
            string.IsNullOrWhiteSpace(NewStudent.Email) ||
            string.IsNullOrWhiteSpace(NewStudent.InitialPassword))
        {
            ModelState.AddModelError(string.Empty, "Complete every account and student field.");
        }
        if (!new EmailAddressAttribute().IsValid(NewStudent.Email))
        {
            ModelState.AddModelError(string.Empty, "Enter a valid student email address.");
        }
        if (NewStudent.InitialPassword.Length < 8)
        {
            ModelState.AddModelError(string.Empty, "Student password must have at least 8 characters.");
        }
        if (NewStudent.YearOfStudy is < 1 or > 8)
        {
            ModelState.AddModelError(string.Empty, "Year of study must be between 1 and 8.");
        }
        return ModelState.IsValid;
    }

    private bool ValidateUpdateStudent()
    {
        if (UpdateStudent.Id <= 0 ||
            string.IsNullOrWhiteSpace(UpdateStudent.StudentNumber) ||
            string.IsNullOrWhiteSpace(UpdateStudent.FirstName) ||
            string.IsNullOrWhiteSpace(UpdateStudent.LastName))
        {
            ModelState.AddModelError(string.Empty, "Complete the required student fields.");
        }
        if (UpdateStudent.YearOfStudy is < 1 or > 8)
        {
            ModelState.AddModelError(string.Empty, "Year of study must be between 1 and 8.");
        }
        return ModelState.IsValid;
    }

    private async Task LoadPageAsync()
    {
        Programmes = await database.Programmes
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync();
        StudyModes = await database.StudyModes
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync();
        Students = await database.Students
            .Include(item => item.ProgrammeRecord)
            .Include(item => item.StudyModeRecord)
            .AsNoTracking()
            .OrderBy(item => item.StudentNumber)
            .ToListAsync();
    }

    public sealed class NewStudentInput
    {
        public string StudentNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int ProgrammeId { get; set; }
        public int StudyModeId { get; set; }
        public int YearOfStudy { get; set; } = 1;
        public string Email { get; set; } = string.Empty;
        public string InitialPassword { get; set; } = string.Empty;
    }

    public sealed class UpdateStudentInput
    {
        public int Id { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int ProgrammeId { get; set; }
        public int StudyModeId { get; set; }
        public int YearOfStudy { get; set; }
        public bool IsAcademicallyEligible { get; set; } = true;
        public string? AcademicHoldReason { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
