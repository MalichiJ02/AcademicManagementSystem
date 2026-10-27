using System.Text;
using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class CourseLecturerAssignmentImportModel(
    AcademicManagementDbContext database) : PageModel
{
    private static readonly string[] RequiredColumns =
    [
        "CourseCode",
        "LecturerStaffNumber",
        "IsPrimary"
    ];

    [BindProperty]
    public IFormFile? CsvFile { get; set; }

    public IReadOnlyList<AssignmentImportResult> ImportResults { get; private set; } = [];

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

        var courses = await database.Courses
            .Where(course => course.IsActive)
            .ToDictionaryAsync(
                course => course.Code,
                StringComparer.OrdinalIgnoreCase);

        var lecturers = await database.Lecturers
            .Where(lecturer => lecturer.IsActive)
            .ToDictionaryAsync(
                lecturer => lecturer.StaffNumber,
                StringComparer.OrdinalIgnoreCase);

        var existingAssignments = await database.CourseLecturerAssignments
            .Where(assignment => assignment.IsActive)
            .ToListAsync();

        var existingAssignmentKeys = existingAssignments
            .Select(assignment => $"{assignment.CourseId}:{assignment.LecturerId}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var coursesWithPrimaryLecturer = existingAssignments
            .Where(assignment => assignment.IsPrimary)
            .Select(assignment => assignment.CourseId)
            .ToHashSet();

        var assignmentKeysInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<AssignmentImportResult>();

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
            .Where(required => !headers.Contains(
                required,
                StringComparer.OrdinalIgnoreCase))
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
                results.Add(new AssignmentImportResult(
                    lineNumber,
                    string.Empty,
                    string.Empty,
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

            var courseCode = row["CourseCode"];
            var staffNumber = row["LecturerStaffNumber"];
            var isPrimaryText = row["IsPrimary"];

            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(courseCode))
            {
                errors.Add("Course code is required.");
            }

            if (string.IsNullOrWhiteSpace(staffNumber))
            {
                errors.Add("Lecturer staff number is required.");
            }

            if (!TryParseBoolean(isPrimaryText, out var isPrimary))
            {
                errors.Add("IsPrimary must be Yes, No, True, False, 1, or 0.");
            }

            courses.TryGetValue(courseCode, out var course);
            lecturers.TryGetValue(staffNumber, out var lecturer);

            if (!string.IsNullOrWhiteSpace(courseCode) && course is null)
            {
                errors.Add($"Active course '{courseCode}' does not exist.");
            }

            if (!string.IsNullOrWhiteSpace(staffNumber) && lecturer is null)
            {
                errors.Add($"Active lecturer '{staffNumber}' does not exist.");
            }

            if (course is not null && lecturer is not null)
            {
                var assignmentKey = $"{course.Id}:{lecturer.Id}";

                if (existingAssignmentKeys.Contains(assignmentKey) ||
                    !assignmentKeysInFile.Add(assignmentKey))
                {
                    errors.Add(
                        $"{lecturer.FullName} is already assigned to {course.Code}.");
                }

                if (isPrimary && coursesWithPrimaryLecturer.Contains(course.Id))
                {
                    errors.Add(
                        $"{course.Code} already has a primary lecturer.");
                }
            }

            if (errors.Count > 0)
            {
                results.Add(new AssignmentImportResult(
                    lineNumber,
                    courseCode,
                    staffNumber,
                    false,
                    string.Join(" ", errors)));

                continue;
            }

            var assignment = new CourseLecturerAssignment
            {
                CourseId = course!.Id,
                LecturerId = lecturer!.Id,
                IsPrimary = isPrimary,
                IsActive = true
            };

            try
            {
                database.CourseLecturerAssignments.Add(assignment);
                await database.SaveChangesAsync();

                existingAssignmentKeys.Add($"{course.Id}:{lecturer.Id}");

                if (isPrimary)
                {
                    coursesWithPrimaryLecturer.Add(course.Id);
                }

                results.Add(new AssignmentImportResult(
                    lineNumber,
                    courseCode,
                    staffNumber,
                    true,
                    $"{lecturer.FullName} was assigned to {course.Code}."));
            }
            catch
            {
                database.CourseLecturerAssignments.Remove(assignment);

                results.Add(new AssignmentImportResult(
                    lineNumber,
                    courseCode,
                    staffNumber,
                    false,
                    "The course–lecturer assignment could not be saved."));
            }
        }

        ImportResults = results;
        return Page();
    }

    private static bool TryParseBoolean(string value, out bool result)
    {
        result = value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                 value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                 value == "1";

        return result ||
               value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
               value == "0";
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

    public sealed record AssignmentImportResult(
        int LineNumber,
        string CourseCode,
        string LecturerStaffNumber,
        bool Success,
        string Message);
}