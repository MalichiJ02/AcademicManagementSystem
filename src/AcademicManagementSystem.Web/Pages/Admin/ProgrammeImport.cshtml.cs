using System.Globalization;
using System.Text;
using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class ProgrammeImportModel(
    AcademicManagementDbContext database) : PageModel
{
    private static readonly string[] RequiredColumns =
    [
        "ProgrammeCode",
        "ProgrammeName",
        "BaseTuition"
    ];

    [BindProperty]
    public IFormFile? CsvFile { get; set; }

    public IReadOnlyList<ProgrammeImportResult> ImportResults { get; private set; } = [];

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

        if (CsvFile.Length > 2 * 1024 * 1024)
        {
            ModelState.AddModelError(string.Empty, "The CSV file must be smaller than 2 MB.");
            return Page();
        }

        var existingCodes = (await database.Programmes
                .Select(programme => programme.Code)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var codesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<ProgrammeImportResult>();

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

        if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Count)
        {
            ModelState.AddModelError(
                string.Empty,
                "The CSV file contains duplicate column headings.");

            return Page();
        }

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
                results.Add(new ProgrammeImportResult(
                    lineNumber,
                    "",
                    false,
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

            var code = row["ProgrammeCode"];
            var name = row["ProgrammeName"];
            var tuitionText = row["BaseTuition"];
            var validationErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(code))
            {
                validationErrors.Add("Programme code is required.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                validationErrors.Add("Programme name is required.");
            }

            if (!decimal.TryParse(
                    tuitionText,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var tuitionAmount) ||
                tuitionAmount < 0)
            {
                validationErrors.Add(
                    "Base tuition must be a valid number greater than or equal to zero.");
            }

            if (!string.IsNullOrWhiteSpace(code) &&
                (existingCodes.Contains(code) || !codesInFile.Add(code)))
            {
                validationErrors.Add($"Programme code '{code}' already exists.");
            }

            if (validationErrors.Count > 0)
            {
                results.Add(new ProgrammeImportResult(
                    lineNumber,
                    code,
                    false,
                    string.Join(" ", validationErrors)));

                continue;
            }

            var programme = new Programme
            {
                Code = code,
                Name = name,
                TuitionAmount = tuitionAmount,
                IsActive = true
            };

            try
            {
                database.Programmes.Add(programme);
                await database.SaveChangesAsync();

                existingCodes.Add(code);

                results.Add(new ProgrammeImportResult(
                    lineNumber,
                    code,
                    true,
                    $"{name} was imported successfully."));
            }
            catch
{
    database.Entry(programme).State = EntityState.Detached;

    results.Add(new ProgrammeImportResult(
        lineNumber,
        code,
        false,
        "The programme could not be saved."));
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

    public sealed record ProgrammeImportResult(
        int LineNumber,
        string ProgrammeCode,
        bool Success,
        string Message);
}