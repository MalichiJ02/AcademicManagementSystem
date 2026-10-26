using System.Text;
using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class CourseImportModel(
    AcademicManagementDbContext database) : PageModel
{
    private static readonly string[] RequiredColumns =
    [
        "CourseCode",
        "CourseTitle",
        "ProgrammeCode",
        "StudyMode",
        "YearLevel",
        "CreditUnits",
        "SemesterOffered",
        "ExpectedEnrollment",
        "CourseType",
        "PrerequisiteCourseCode"
    ];

    [BindProperty]
    public IFormFile? CsvFile { get; set; }

    public IReadOnlyList<CourseImportResult> ImportResults { get; private set; } = [];

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

        var programmes = (await database.Programmes
                .Where(programme => programme.IsActive)
                .ToListAsync())
            .ToDictionary(
                programme => programme.Code.Trim(),
                programme => programme,
                StringComparer.OrdinalIgnoreCase);

        var studyModes = (await database.StudyModes
                .Where(studyMode => studyMode.IsActive)
                .ToListAsync())
            .ToDictionary(
                studyMode => studyMode.Name.Trim(),
                studyMode => studyMode,
                StringComparer.OrdinalIgnoreCase);

        var existingCourses = (await database.Courses.ToListAsync())
            .ToDictionary(
                course => course.Code.Trim(),
                course => course,
                StringComparer.OrdinalIgnoreCase);

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

        var rows = new List<ImportCourseRow>();
        var courseCodesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                rows.Add(new ImportCourseRow
                {
                    LineNumber = lineNumber,
                    Errors = ["The number of values does not match the column headings."]
                });

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

            var importRow = new ImportCourseRow
            {
                LineNumber = lineNumber,
                CourseCode = row["CourseCode"],
                CourseTitle = row["CourseTitle"],
                ProgrammeCode = row["ProgrammeCode"],
                StudyMode = row["StudyMode"],
                SemesterOffered = row["SemesterOffered"],
                CourseType = row["CourseType"],
                PrerequisiteCourseCode = row["PrerequisiteCourseCode"]
            };

            if (!int.TryParse(row["YearLevel"], out var yearLevel) ||
                yearLevel < 1 || yearLevel > 10)
            {
                importRow.Errors.Add("Year level must be a number from 1 to 10.");
            }
            else
            {
                importRow.YearLevel = yearLevel;
            }

            if (!int.TryParse(row["CreditUnits"], out var creditUnits) ||
                creditUnits < 1 || creditUnits > 30)
            {
                importRow.Errors.Add("Credit units must be a number from 1 to 30.");
            }
            else
            {
                importRow.CreditUnits = creditUnits;
            }

            if (!int.TryParse(row["ExpectedEnrollment"], out var expectedEnrollment) ||
                expectedEnrollment < 0)
            {
                importRow.Errors.Add(
                    "Expected enrolment must be a whole number greater than or equal to zero.");
            }
            else
            {
                importRow.ExpectedEnrollment = expectedEnrollment;
            }

            if (string.IsNullOrWhiteSpace(importRow.CourseCode))
            {
                importRow.Errors.Add("Course code is required.");
            }
            else if (existingCourses.ContainsKey(importRow.CourseCode) ||
                     !courseCodesInFile.Add(importRow.CourseCode))
            {
                importRow.Errors.Add(
                    $"Course code '{importRow.CourseCode}' already exists.");
            }

            if (string.IsNullOrWhiteSpace(importRow.CourseTitle))
            {
                importRow.Errors.Add("Course title is required.");
            }

            if (!programmes.TryGetValue(importRow.ProgrammeCode, out var programme))
            {
                importRow.Errors.Add(
                    $"Active programme code '{importRow.ProgrammeCode}' does not exist.");
            }
            else
            {
                importRow.Programme = programme;
            }

            if (!studyModes.TryGetValue(importRow.StudyMode, out var studyMode))
            {
                importRow.Errors.Add(
                    $"Active study mode '{importRow.StudyMode}' does not exist.");
            }
            else
            {
                importRow.StudyModeRecord = studyMode;
            }

            if (!IsValidSemester(importRow.SemesterOffered))
            {
                importRow.Errors.Add(
                    "Semester offered must be Semester 1, Semester 2, or Both.");
            }

            if (string.IsNullOrWhiteSpace(importRow.CourseType))
            {
                importRow.Errors.Add("Course type is required, for example Core or Elective.");
            }

            if (importRow.PrerequisiteCourseCode.Equals(
                    importRow.CourseCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                importRow.Errors.Add("A course cannot be its own prerequisite.");
            }

            rows.Add(importRow);
        }

        var validCourseCodes = rows
            .Where(row => row.Errors.Count == 0)
            .Select(row => row.CourseCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows.Where(row =>
                     row.Errors.Count == 0 &&
                     !string.IsNullOrWhiteSpace(row.PrerequisiteCourseCode)))
        {
            var prerequisiteExists = existingCourses.ContainsKey(row.PrerequisiteCourseCode) ||
                                     validCourseCodes.Contains(row.PrerequisiteCourseCode);

            if (!prerequisiteExists)
            {
                row.Errors.Add(
                    $"Prerequisite course '{row.PrerequisiteCourseCode}' does not exist " +
                    "and is not included in this import file.");
            }
        }

        foreach (var row in rows.Where(row => row.Errors.Count == 0))
        {
            var course = new Course
            {
                Code = row.CourseCode,
                Title = row.CourseTitle,
                ProgrammeId = row.Programme!.Id,
                StudyModeId = row.StudyModeRecord!.Id,
                CreditUnits = row.CreditUnits,
                YearLevel = row.YearLevel,
                SemesterOffered = row.SemesterOffered,
                PrerequisiteCourseCode = string.IsNullOrWhiteSpace(row.PrerequisiteCourseCode)
                    ? null
                    : row.PrerequisiteCourseCode,
                ExpectedEnrollment = row.ExpectedEnrollment,
                CourseType = row.CourseType,
                ScheduleKey = string.Empty,
                Venue = string.Empty,
                IsActive = true
            };

            try
            {
                database.Courses.Add(course);
                await database.SaveChangesAsync();

                row.Course = course;
                existingCourses[course.Code] = course;
                row.Message = $"{course.Title} was imported successfully.";
            }
            catch
            {
                database.Entry(course).State = EntityState.Detached;
                row.Errors.Add("The course could not be saved.");
            }
        }

        foreach (var row in rows.Where(row =>
                     row.Course is not null &&
                     !string.IsNullOrWhiteSpace(row.PrerequisiteCourseCode)))
        {
            if (!existingCourses.TryGetValue(
                    row.PrerequisiteCourseCode,
                    out var prerequisiteCourse))
            {
                row.Message += " Course imported, but its prerequisite link was not created.";
                continue;
            }

            var relationship = new CoursePrerequisite
            {
                CourseId = row.Course!.Id,
                PrerequisiteCourseId = prerequisiteCourse.Id
            };

            try
            {
                database.CoursePrerequisites.Add(relationship);
                await database.SaveChangesAsync();
            }
            catch
            {
                database.Entry(relationship).State = EntityState.Detached;
                row.Message += " Course imported, but its prerequisite link was not created.";
            }
        }

        ImportResults = rows
            .Select(row => new CourseImportResult(
                row.LineNumber,
                row.CourseCode,
                row.Course is not null,
                row.Course is not null
                    ? row.Message
                    : string.Join(" ", row.Errors)))
            .ToList();

        return Page();
    }

    private static bool IsValidSemester(string value) =>
        value.Equals("Semester 1", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Semester 2", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Both", StringComparison.OrdinalIgnoreCase);

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

    private sealed class ImportCourseRow
    {
        public int LineNumber { get; init; }
        public string CourseCode { get; init; } = string.Empty;
        public string CourseTitle { get; init; } = string.Empty;
        public string ProgrammeCode { get; init; } = string.Empty;
        public string StudyMode { get; init; } = string.Empty;
        public int YearLevel { get; set; }
        public int CreditUnits { get; set; }
        public string SemesterOffered { get; init; } = string.Empty;
        public int ExpectedEnrollment { get; set; }
        public string CourseType { get; init; } = string.Empty;
        public string PrerequisiteCourseCode { get; init; } = string.Empty;
        public Programme? Programme { get; set; }
        public StudyMode? StudyModeRecord { get; set; }
        public Course? Course { get; set; }
        public List<string> Errors { get; set; } = [];
        public string Message { get; set; } = string.Empty;
    }

    public sealed record CourseImportResult(
        int LineNumber,
        string CourseCode,
        bool Success,
        string Message);
}