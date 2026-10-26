using System.Text;
using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class LecturerImportModel(
    AcademicManagementDbContext database) : PageModel
{
    private static readonly string[] RequiredColumns =
    [
        "StaffNumber",
        "FullName",
        "Specialisation",
        "AvailableDays"
    ];

    private static readonly string[] ValidDays =
    [
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday"
    ];

    [BindProperty]
    public IFormFile? CsvFile { get; set; }

    public IReadOnlyList<LecturerImportResult> ImportResults { get; private set; } = [];

    public int SuccessfulImports => ImportResults.Count(result => result.Success);

    public int FailedImports => ImportResults.Count(result => !result.Success);

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

        var existingStaffNumbers = (await database.Lecturers
                .Select(lecturer => lecturer.StaffNumber)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var staffNumbersInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<LecturerImportResult>();

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

        var separator = DetectSeparator(headerLine);
        var headers = ParseCsvLine(headerLine, separator)
            .Select(header => header.Trim())
            .ToList();

        var missingColumns = RequiredColumns
            .Where(required => !headers.Contains(required, StringComparer.OrdinalIgnoreCase))
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

            var values = ParseCsvLine(line, separator);

            if (values.Count != headers.Count)
            {
                results.Add(new LecturerImportResult(
                    lineNumber, "", false,
                    "The number of values does not match the column headings."));

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

            var staffNumber = row["StaffNumber"];
            var fullName = row["FullName"];
            var specialisation = row["Specialisation"];
            var availableDays = row["AvailableDays"];
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(staffNumber))
            {
                errors.Add("Staff number is required.");
            }
            else if (existingStaffNumbers.Contains(staffNumber) ||
                     !staffNumbersInFile.Add(staffNumber))
            {
                errors.Add($"Staff number '{staffNumber}' already exists.");
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                errors.Add("Full name is required.");
            }

            if (string.IsNullOrWhiteSpace(specialisation))
            {
                errors.Add("Specialisation is required.");
            }

            var days = availableDays
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (days.Length == 0)
            {
                errors.Add("Enter at least one available day.");
            }
            else if (days.Any(day =>
                         !ValidDays.Contains(day, StringComparer.OrdinalIgnoreCase)))
            {
                errors.Add(
                    "Available days must use Monday to Friday, separated by | symbols.");
            }

            if (errors.Count > 0)
            {
                results.Add(new LecturerImportResult(
                    lineNumber, staffNumber, false, string.Join(" ", errors)));

                continue;
            }

            var lecturer = new Lecturer
            {
                StaffNumber = staffNumber,
                FullName = fullName,
                Specialisation = specialisation,
                AvailableDays = string.Join("|", days),
                IsActive = true
            };

            try
            {
                database.Lecturers.Add(lecturer);
                await database.SaveChangesAsync();

                existingStaffNumbers.Add(staffNumber);

                results.Add(new LecturerImportResult(
                    lineNumber,
                    staffNumber,
                    true,
                    $"{fullName} was imported successfully."));
            }
            catch
            {
                database.Entry(lecturer).State = EntityState.Detached;

                results.Add(new LecturerImportResult(
                    lineNumber,
                    staffNumber,
                    false,
                    "The lecturer could not be saved."));
            }
        }

        ImportResults = results;
        return Page();
    }

    private static char DetectSeparator(string headerLine) =>
        headerLine.Count(character => character == ';') >
        headerLine.Count(character => character == ',')
            ? ';'
            : ',';

    private static List<string> ParseCsvLine(string line, char separator)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var insideQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (character == '"')
            {
                insideQuotes = !insideQuotes;
            }
            else if (character == separator && !insideQuotes)
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

    public sealed record LecturerImportResult(
        int LineNumber,
        string StaffNumber,
        bool Success,
        string Message);
}