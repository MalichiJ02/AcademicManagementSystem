using System.ComponentModel.DataAnnotations;
using System.Text;
using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class StudentImportModel(
    AcademicManagementDbContext database,
    UserManager<ApplicationUser> userManager) : PageModel
{
    private const string DefaultStudentPassword = "Test1234";

    private static readonly string[] RequiredColumns =
    [
        "StudentId",
        "FirstName",
        "LastName",
        "Email",
        "ProgrammeCode",
        "StudyMode",
        "YearOfStudy"
    ];

    [BindProperty]
    public IFormFile? CsvFile { get; set; }

    public IReadOnlyList<CsvImportResult> ImportResults { get; private set; } = [];

    public int SuccessfulImports =>
        ImportResults.Count(result => result.Success);

    public int FailedImports =>
        ImportResults.Count(result => !result.Success);

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (CsvFile is null || CsvFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Select a CSV file to import.");
            return Page();
        }

        if (!Path.GetExtension(CsvFile.FileName)
                .Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Only CSV files are allowed.");
            return Page();
        }

        if (CsvFile.Length > 2 * 1024 * 1024)
        {
            ModelState.AddModelError(
                string.Empty,
                "The CSV file must be smaller than 2 MB.");

            return Page();
        }

        var programmes = (await database.Programmes.ToListAsync())
            .ToDictionary(
                programme => programme.Code.Trim(),
                programme => programme,
                StringComparer.OrdinalIgnoreCase);

        var studyModes = (await database.StudyModes.ToListAsync())
            .ToDictionary(
                studyMode => studyMode.Name.Trim(),
                studyMode => studyMode,
                StringComparer.OrdinalIgnoreCase);

        var existingStudentNumbers = (await database.Students
                .Select(student => student.StudentNumber)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var studentIdsInFile = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var emailsInFile = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var results = new List<CsvImportResult>();

        using var stream = CsvFile.OpenReadStream();
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        var headerLine = await reader.ReadLineAsync();

        if (string.IsNullOrWhiteSpace(headerLine))
        {
            ModelState.AddModelError(string.Empty, "The CSV file is empty.");
            return Page();
        }

        var headers = ParseCsvLine(headerLine)
            .Select(header => header.Trim())
            .ToList();

        if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count()
            != headers.Count)
        {
            ModelState.AddModelError(
                string.Empty,
                "The CSV file contains duplicate column headings.");

            return Page();
        }

        var missingColumns = RequiredColumns
            .Where(required =>
                !headers.Contains(required, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (missingColumns.Count > 0)
        {
            ModelState.AddModelError(
                string.Empty,
                $"The CSV file is missing: {string.Join(", ", missingColumns)}.");

            return Page();
        }

        var lineNumber = 1;
        string? line;

        while ((line = await reader.ReadLineAsync()) is not null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseCsvLine(line);

            if (values.Count != headers.Count)
            {
                results.Add(new CsvImportResult(
                    lineNumber,
                    "",
                    false,
                    "The number of values does not match the column headings.",
                    null));

                continue;
            }

            var row = headers
                .Select((header, index) => new
                {
                    Header = header,
                    Value = values[index].Trim()
                })
                .ToDictionary(
                    item => item.Header,
                    item => item.Value,
                    StringComparer.OrdinalIgnoreCase);

            var studentNumber = row["StudentId"];
            var firstName = row["FirstName"];
            var lastName = row["LastName"];
            var email = row["Email"];
            var programmeCode = row["ProgrammeCode"];
            var studyModeName = row["StudyMode"];

            var validationErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(studentNumber))
            {
                validationErrors.Add("Student ID is required.");
            }

            if (string.IsNullOrWhiteSpace(firstName))
            {
                validationErrors.Add("First name is required.");
            }

            if (string.IsNullOrWhiteSpace(lastName))
            {
                validationErrors.Add("Last name is required.");
            }

            if (!new EmailAddressAttribute().IsValid(email))
            {
                validationErrors.Add("A valid email address is required.");
            }

            if (!programmes.TryGetValue(programmeCode, out var programme))
            {
                validationErrors.Add(
                    $"Programme code '{programmeCode}' does not exist.");
            }

            if (!studyModes.TryGetValue(studyModeName, out var studyMode))
            {
                validationErrors.Add(
                    $"Study mode '{studyModeName}' does not exist.");
            }

            if (!int.TryParse(row["YearOfStudy"], out var yearOfStudy) ||
                yearOfStudy < 1 ||
                yearOfStudy > 10)
            {
                validationErrors.Add(
                    "Year of study must be a number from 1 to 10.");
            }

            if (existingStudentNumbers.Contains(studentNumber) ||
                !studentIdsInFile.Add(studentNumber))
            {
                validationErrors.Add(
                    $"Student ID '{studentNumber}' already exists.");
            }

            if (!string.IsNullOrWhiteSpace(email) &&
                !emailsInFile.Add(email))
            {
                validationErrors.Add(
                    $"Email '{email}' appears more than once in this file.");
            }

            if (!string.IsNullOrWhiteSpace(studentNumber) &&
                await userManager.FindByNameAsync(studentNumber) is not null)
            {
                validationErrors.Add(
                    $"A login account already uses Student ID '{studentNumber}'.");
            }

            if (!string.IsNullOrWhiteSpace(email) &&
                await userManager.FindByEmailAsync(email) is not null)
            {
                validationErrors.Add(
                    $"A login account already uses '{email}'.");
            }

            if (validationErrors.Count > 0)
            {
                results.Add(new CsvImportResult(
                    lineNumber,
                    studentNumber,
                    false,
                    string.Join(" ", validationErrors),
                    null));

                continue;
            }

            var temporaryPassword = DefaultStudentPassword;

            var user = new ApplicationUser
            {
                UserName = studentNumber,
                Email = email,
                FullName = $"{firstName} {lastName}",
                IsActive = true
            };

            var createUserResult = await userManager.CreateAsync(
                user,
                temporaryPassword);

            if (!createUserResult.Succeeded)
            {
                results.Add(new CsvImportResult(
                    lineNumber,
                    studentNumber,
                    false,
                    FormatIdentityErrors(createUserResult.Errors),
                    null));

                continue;
            }

            var addToRoleResult = await userManager.AddToRoleAsync(
                user,
                "Student");

            if (!addToRoleResult.Succeeded)
            {
                await userManager.DeleteAsync(user);

                results.Add(new CsvImportResult(
                    lineNumber,
                    studentNumber,
                    false,
                    $"Student role could not be assigned. " +
                    FormatIdentityErrors(addToRoleResult.Errors),
                    null));

                continue;
            }

            var student = new AcademicManagementSystem.Web.Models.Student
            {
                StudentNumber = studentNumber,
                IdentityUserId = user.Id,
                FirstName = firstName,
                LastName = lastName,
                Programme = programme!.Name,
                ProgrammeId = programme.Id,
                StudyMode = studyMode!.Name,
                StudyModeId = studyMode.Id,
                YearOfStudy = yearOfStudy,
                IsAcademicallyEligible = true,
                ProgressionStatus = "Normal",
                IsActive = true
            };

            try
            {
                database.Students.Add(student);
                await database.SaveChangesAsync();

                user.StudentId = student.Id;

                var updateUserResult = await userManager.UpdateAsync(user);

                if (!updateUserResult.Succeeded)
                {
                    database.Students.Remove(student);
                    await database.SaveChangesAsync();
                    await userManager.DeleteAsync(user);

                    results.Add(new CsvImportResult(
                        lineNumber,
                        studentNumber,
                        false,
                        $"Student account could not be linked. " +
                        FormatIdentityErrors(updateUserResult.Errors),
                        null));

                    continue;
                }

                existingStudentNumbers.Add(studentNumber);

                results.Add(new CsvImportResult(
                    lineNumber,
                    studentNumber,
                    true,
                    $"{firstName} {lastName} was imported successfully.",
                    temporaryPassword));
            }
            catch
            {
                database.Students.Remove(student);
                await database.SaveChangesAsync();
                await userManager.DeleteAsync(user);

                results.Add(new CsvImportResult(
                    lineNumber,
                    studentNumber,
                    false,
                    "The student could not be saved. No account was created.",
                    null));
            }
        }

        ImportResults = results;
        return Page();
    }

    private static string FormatIdentityErrors(
        IEnumerable<IdentityError> errors) =>
        string.Join(" ", errors.Select(error => error.Description));

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var insideQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (character == '"')
            {
                if (insideQuotes &&
                    index + 1 < line.Length &&
                    line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (character == ',' && !insideQuotes)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }

        values.Add(value.ToString());
        return values;
    }

    public sealed record CsvImportResult(
        int LineNumber,
        string StudentNumber,
        bool Success,
        string Message,
        string? TemporaryPassword);
}