using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class AcademicModel(
    AcademicManagementDbContext database,
    AcademicSettingsService settingsService) : PageModel
{
    [BindProperty] public AcademicSettingsInput SettingsInput { get; set; } = new();
    [BindProperty] public ProgrammeInput NewProgramme { get; set; } = new();
    [BindProperty] public SchoolInput NewSchool { get; set; } = new();
    [BindProperty] public StudyModeInput NewStudyMode { get; set; } = new();
    [BindProperty] public CourseInput NewCourse { get; set; } = new();
    [BindProperty] public PrerequisiteInput NewPrerequisite { get; set; } = new();
    [BindProperty] public LecturerInput NewLecturer { get; set; } = new();
    [BindProperty] public RoomInput NewRoom { get; set; } = new();

    public AcademicSettings Settings { get; private set; } = new();
    public IReadOnlyList<Programme> Programmes { get; private set; } = [];
    public IReadOnlyList<School> Schools { get; private set; } = [];
    public IReadOnlyList<StudyMode> StudyModes { get; private set; } = [];
    public IReadOnlyList<Course> Courses { get; private set; } = [];
    public IReadOnlyList<CoursePrerequisite> Prerequisites { get; private set; } = [];
    public IReadOnlyList<Lecturer> Lecturers { get; private set; } = [];
    public IReadOnlyList<Room> Rooms { get; private set; } = [];

    public string Section { get; private set; } = "rules";

    public async Task OnGetAsync(string? section)
    {
        Section = section switch
        {
            "schools" or "programmes" or "study-modes" or "courses" or "resources" or "rooms" or "imports" => section,
            _ => "rules"
        };
        await LoadPageAsync();
        SettingsInput = ToInput(Settings);
    }

    public async Task<IActionResult> OnPostSaveSettingsAsync()
    {
        Section = "rules";
        if (!ValidateSettings())
        {
            await LoadPageAsync();
            return Page();
        }

        var settings = await settingsService.GetAsync();
        settings.AcademicYear = SettingsInput.AcademicYear.Trim();
        settings.Semester = SettingsInput.Semester.Trim();
        settings.IsRegistrationOpen = SettingsInput.IsRegistrationOpen;
        settings.IsLateRegistrationOpen = SettingsInput.IsLateRegistrationOpen;
        settings.RegistrationPaymentPercentage =
            SettingsInput.RegistrationPaymentPercentage;
        settings.ExamSlipPaymentPercentage =
            SettingsInput.ExamSlipPaymentPercentage;
        settings.ResultsPaymentPercentage =
            SettingsInput.ResultsPaymentPercentage;
        settings.MaximumTotalCourses = SettingsInput.MaximumTotalCourses;
        settings.MaximumCarryCourses = SettingsInput.MaximumCarryCourses;
        settings.FailedCoursesForRestriction =
            SettingsInput.FailedCoursesForRestriction;
        settings.LateRegistrationFee = SettingsInput.LateRegistrationFee;
        settings.UpdatedAt = DateTime.UtcNow;

        await database.SaveChangesAsync();

        TempData["Success"] = "Academic and financial rules were updated.";
        return RedirectToPage(new { section = Section });
    }

    public async Task<IActionResult> OnPostAddSchoolAsync()
    {
        Section = "schools";
        if (string.IsNullOrWhiteSpace(NewSchool.Code) ||
            string.IsNullOrWhiteSpace(NewSchool.Name))
        {
            ModelState.AddModelError(
                string.Empty,
                "Enter both a school code and school name.");

            await LoadPageAsync();
            return Page();
        }

        var code = NewSchool.Code.Trim().ToUpperInvariant();

        if (await database.Schools.AnyAsync(item => item.Code == code))
        {
            ModelState.AddModelError(string.Empty, "That school code already exists.");
            await LoadPageAsync();
            return Page();
        }

        database.Schools.Add(new School
        {
            Code = code,
            Name = NewSchool.Name.Trim(),
            IsActive = true
        });

        await database.SaveChangesAsync();

        TempData["Success"] = "School added.";
        return RedirectToPage(new { section = Section });
    }

    public async Task<IActionResult> OnPostAddProgrammeAsync()
    {
        Section = "programmes";
        if (string.IsNullOrWhiteSpace(NewProgramme.Code) ||
            string.IsNullOrWhiteSpace(NewProgramme.Name) ||
            NewProgramme.TuitionAmount <= 0m)
        {
            ModelState.AddModelError(
                string.Empty,
                "Enter a programme code, name, and tuition amount greater than zero.");

            await LoadPageAsync();
            return Page();
        }

        School? school = null;

        if (NewProgramme.SchoolId > 0)
        {
            school = await database.Schools.SingleOrDefaultAsync(item =>
                item.Id == NewProgramme.SchoolId && item.IsActive);

            if (school is null)
            {
                ModelState.AddModelError(string.Empty, "Choose a valid active school.");
                await LoadPageAsync();
                return Page();
            }
        }

        var code = NewProgramme.Code.Trim().ToUpperInvariant();

        if (await database.Programmes.AnyAsync(item => item.Code == code))
        {
            ModelState.AddModelError(string.Empty, "That programme code already exists.");
            await LoadPageAsync();
            return Page();
        }

        database.Programmes.Add(new Programme
        {
            Code = code,
            Name = NewProgramme.Name.Trim(),
            TuitionAmount = NewProgramme.TuitionAmount,
            SchoolId = school?.Id,
            IsActive = true
        });

        await database.SaveChangesAsync();

        TempData["Success"] = "Programme added.";
        return RedirectToPage(new { section = Section });
    }

    public async Task<IActionResult> OnPostAssignProgrammeSchoolAsync(
        int programmeId,
        int? schoolId)
    {
        Section = "programmes";
        var programme = await database.Programmes.FindAsync(programmeId);

        if (programme is null)
        {
            TempData["Error"] = "Programme not found.";
            return RedirectToPage(new { section = Section });
        }

        if (schoolId is null or <= 0)
        {
            programme.SchoolId = null;
            await database.SaveChangesAsync();

            TempData["Success"] = $"{programme.Code} was marked as unassigned.";
            return RedirectToPage(new { section = Section });
        }

        var school = await database.Schools.SingleOrDefaultAsync(item =>
            item.Id == schoolId.Value && item.IsActive);

        if (school is null)
        {
            TempData["Error"] = "Choose a valid active school.";
            return RedirectToPage(new { section = Section });
        }

        programme.SchoolId = school.Id;
        await database.SaveChangesAsync();

        TempData["Success"] = $"{programme.Code} was assigned to {school.Name}.";
        return RedirectToPage(new { section = Section });
    }

    public async Task<IActionResult> OnPostUpdateProgrammeAsync(
        int programmeId,
        string name,
        decimal tuitionAmount,
        bool isActive)
    {
        Section = "programmes";
        var programme = await database.Programmes.FindAsync(programmeId);

        if (programme is null ||
            string.IsNullOrWhiteSpace(name) ||
            tuitionAmount <= 0m)
        {
            TempData["Error"] =
                "A valid programme name and tuition amount are required.";
            return RedirectToPage(new { section = Section });
        }

        programme.Name = name.Trim();
        programme.TuitionAmount = tuitionAmount;
        programme.IsActive = isActive;

        await database.SaveChangesAsync();

        TempData["Success"] =
            $"Programme settings for {programme.Code} were updated.";

        return RedirectToPage(new { section = Section });
    }

    public async Task<IActionResult> OnPostAddStudyModeAsync()
    {
        Section = "study-modes";
        if (string.IsNullOrWhiteSpace(NewStudyMode.Name) ||
            string.IsNullOrWhiteSpace(NewStudyMode.Description) ||
            string.IsNullOrWhiteSpace(NewStudyMode.PaymentAccountName) ||
            string.IsNullOrWhiteSpace(NewStudyMode.PaymentAccountReference) ||
            NewStudyMode.MaximumCourses is < 1 or > 4 ||
            NewStudyMode.AdditionalFee < 0m)
        {
            ModelState.AddModelError(
                string.Empty,
                "Complete the study-mode details. Maximum courses must be between 1 and 4.");

            await LoadPageAsync();
            return Page();
        }

        var name = NewStudyMode.Name.Trim();

        if (await database.StudyModes.AnyAsync(item => item.Name == name))
        {
            ModelState.AddModelError(string.Empty, "That study mode already exists.");
            await LoadPageAsync();
            return Page();
        }

        database.StudyModes.Add(new StudyMode
        {
            Name = name,
            Description = NewStudyMode.Description.Trim(),
            MaximumCourses = NewStudyMode.MaximumCourses,
            AdditionalFee = NewStudyMode.AdditionalFee,
            PaymentAccountName = NewStudyMode.PaymentAccountName.Trim(),
            PaymentAccountReference =
                NewStudyMode.PaymentAccountReference.Trim(),
            IsActive = true
        });

        await database.SaveChangesAsync();

        TempData["Success"] = "Study mode added.";
        return RedirectToPage(new { section = Section });
    }

    public async Task<IActionResult> OnPostUpdateStudyModeAsync(
        int studyModeId,
        string description,
        int maximumCourses,
        decimal additionalFee,
        string paymentAccountName,
        string paymentAccountReference,
        bool isActive)
    {
        Section = "study-modes";
        var studyMode = await database.StudyModes.FindAsync(studyModeId);

        if (studyMode is null ||
            string.IsNullOrWhiteSpace(description) ||
            string.IsNullOrWhiteSpace(paymentAccountName) ||
            string.IsNullOrWhiteSpace(paymentAccountReference) ||
            maximumCourses is < 1 or > 4 ||
            additionalFee < 0m)
        {
            TempData["Error"] =
                "Complete the study-mode description, payment account, course load, and fee.";

            return RedirectToPage(new { section = Section });
        }

        studyMode.Description = description.Trim();
        studyMode.MaximumCourses = maximumCourses;
        studyMode.AdditionalFee = additionalFee;
        studyMode.PaymentAccountName = paymentAccountName.Trim();
        studyMode.PaymentAccountReference = paymentAccountReference.Trim();
        studyMode.IsActive = isActive;

        await database.SaveChangesAsync();

        TempData["Success"] =
            $"Study mode settings for {studyMode.Name} were updated.";

        return RedirectToPage(new { section = Section });
    }

    public async Task<IActionResult> OnPostAddCourseAsync()
    {
        Section = "courses";
        if (string.IsNullOrWhiteSpace(NewCourse.Code) ||
            string.IsNullOrWhiteSpace(NewCourse.Title) ||
            NewCourse.ProgrammeId <= 0 ||
            NewCourse.CreditUnits is < 1 or > 30 ||
            NewCourse.YearLevel is < 1 or > 8)
        {
            ModelState.AddModelError(
                string.Empty,
                "Complete course code, title, programme, credit units, and year level.");

            await LoadPageAsync();
            return Page();
        }

        var programme = await database.Programmes.SingleOrDefaultAsync(item =>
            item.Id == NewCourse.ProgrammeId && item.IsActive);

        if (programme is null)
        {
            ModelState.AddModelError(string.Empty, "Choose an active programme.");
            await LoadPageAsync();
            return Page();
        }

        StudyMode? studyMode = null;

        if (NewCourse.StudyModeId > 0)
        {
            studyMode = await database.StudyModes.SingleOrDefaultAsync(item =>
                item.Id == NewCourse.StudyModeId && item.IsActive);

            if (studyMode is null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Choose a valid study mode or select all study modes.");

                await LoadPageAsync();
                return Page();
            }
        }

        var code = NewCourse.Code.Trim().ToUpperInvariant();

        if (await database.Courses.AnyAsync(item => item.Code == code))
        {
            ModelState.AddModelError(string.Empty, "That course code already exists.");
            await LoadPageAsync();
            return Page();
        }

        database.Courses.Add(new Course
        {
            Code = code,
            Title = NewCourse.Title.Trim(),
            ProgrammeId = programme.Id,
            StudyModeId = studyMode?.Id,
            CreditUnits = NewCourse.CreditUnits,
            YearLevel = NewCourse.YearLevel,
            SemesterOffered = NewCourse.SemesterOffered,
            ScheduleKey = "Awaiting timetable allocation",
            Venue = "Unassigned",
            ExpectedEnrollment = Math.Max(1, NewCourse.ExpectedEnrollment),
            CourseType = NewCourse.CourseType.Trim().Length == 0
                ? "Core"
                : NewCourse.CourseType.Trim(),
            IsActive = true
        });

        await database.SaveChangesAsync();

        TempData["Success"] =
            "Course added. Define its prerequisites and timetable next.";

        return RedirectToPage(new { section = Section });
    }

    public async Task<IActionResult> OnPostToggleCourseAsync(
        int courseId,
        bool isActive)
    {
        Section = "courses";
        var course = await database.Courses.FindAsync(courseId);

        if (course is null)
        {
            TempData["Error"] = "Course not found.";
            return RedirectToPage(new { section = Section });
        }

        course.IsActive = isActive;
        await database.SaveChangesAsync();

        TempData["Success"] =
            $"{course.Code} was {(isActive ? "activated" : "deactivated")}.";

        return RedirectToPage(new { section = Section });
    }

    public async Task<IActionResult> OnPostAddPrerequisiteAsync()
    {
        Section = "courses";
        if (NewPrerequisite.CourseId <= 0 ||
            NewPrerequisite.PrerequisiteCourseId <= 0 ||
            NewPrerequisite.CourseId == NewPrerequisite.PrerequisiteCourseId)
        {
            ModelState.AddModelError(string.Empty, "Choose two different courses.");
            await LoadPageAsync();
            return Page();
        }

        var exists = await database.CoursePrerequisites.AnyAsync(item =>
            item.CourseId == NewPrerequisite.CourseId &&
            item.PrerequisiteCourseId ==
            NewPrerequisite.PrerequisiteCourseId);

        if (exists)
        {
            ModelState.AddModelError(
                string.Empty,
                "That prerequisite is already defined.");

            await LoadPageAsync();
            return Page();
        }

        database.CoursePrerequisites.Add(new CoursePrerequisite
        {
            CourseId = NewPrerequisite.CourseId,
            PrerequisiteCourseId =
                NewPrerequisite.PrerequisiteCourseId
        });

        await database.SaveChangesAsync();

        TempData["Success"] = "Course prerequisite added.";
        return RedirectToPage(new { section = Section });
    }

    public async Task<IActionResult> OnPostRemovePrerequisiteAsync(
        int courseId,
        int prerequisiteCourseId)
    {
        Section = "courses";
        var link = await database.CoursePrerequisites.FindAsync(
            courseId,
            prerequisiteCourseId);

        if (link is not null)
        {
            database.CoursePrerequisites.Remove(link);
            await database.SaveChangesAsync();
            TempData["Success"] = "Course prerequisite removed.";
        }

        return RedirectToPage(new { section = Section });
    }

    public async Task<IActionResult> OnPostAddLecturerAsync()
    {
        Section = "resources";
        if (string.IsNullOrWhiteSpace(NewLecturer.StaffNumber) ||
            string.IsNullOrWhiteSpace(NewLecturer.FullName) ||
            string.IsNullOrWhiteSpace(NewLecturer.Specialisation) ||
            string.IsNullOrWhiteSpace(NewLecturer.AvailableDays))
        {
            ModelState.AddModelError(
                string.Empty,
                "Complete all lecturer details.");

            await LoadPageAsync();
            return Page();
        }

        var staffNumber = NewLecturer.StaffNumber
            .Trim()
            .ToUpperInvariant();

        if (await database.Lecturers.AnyAsync(item =>
                item.StaffNumber == staffNumber))
        {
            ModelState.AddModelError(
                string.Empty,
                "That staff number already exists.");

            await LoadPageAsync();
            return Page();
        }

        database.Lecturers.Add(new Lecturer
        {
            StaffNumber = staffNumber,
            FullName = NewLecturer.FullName.Trim(),
            Specialisation = NewLecturer.Specialisation.Trim(),
            AvailableDays = NewLecturer.AvailableDays.Trim(),
            IsActive = true
        });

        await database.SaveChangesAsync();

        TempData["Success"] = "Lecturer added.";
        return RedirectToPage(new { section = Section });
    }

    public async Task<IActionResult> OnPostAddRoomAsync()
    {
        Section = "rooms";
        if (string.IsNullOrWhiteSpace(NewRoom.Code) ||
            string.IsNullOrWhiteSpace(NewRoom.Name) ||
            string.IsNullOrWhiteSpace(NewRoom.RoomType) ||
            NewRoom.Capacity <= 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "Complete room code, name, type, and capacity.");

            await LoadPageAsync();
            return Page();
        }

        var code = NewRoom.Code.Trim().ToUpperInvariant();

        if (await database.Rooms.AnyAsync(item => item.Code == code))
        {
            ModelState.AddModelError(string.Empty, "That room code already exists.");
            await LoadPageAsync();
            return Page();
        }

        database.Rooms.Add(new Room
        {
            Code = code,
            Name = NewRoom.Name.Trim(),
            Capacity = NewRoom.Capacity,
            RoomType = NewRoom.RoomType.Trim(),
            IsActive = true
        });

        await database.SaveChangesAsync();

        TempData["Success"] = "Room added.";
        return RedirectToPage(new { section = Section });
    }

    private bool ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(SettingsInput.AcademicYear) ||
            string.IsNullOrWhiteSpace(SettingsInput.Semester))
        {
            ModelState.AddModelError(
                string.Empty,
                "Academic year and semester are required.");
        }

        var percentages = new[]
        {
            SettingsInput.RegistrationPaymentPercentage,
            SettingsInput.ExamSlipPaymentPercentage,
            SettingsInput.ResultsPaymentPercentage
        };

        if (percentages.Any(value => value is < 0m or > 100m))
        {
            ModelState.AddModelError(
                string.Empty,
                "Payment thresholds must be between 0% and 100%.");
        }

        if (SettingsInput.MaximumTotalCourses is < 1 or > 4 ||
            SettingsInput.MaximumCarryCourses is < 0 or > 4 ||
            SettingsInput.MaximumCarryCourses >
            SettingsInput.MaximumTotalCourses ||
            SettingsInput.FailedCoursesForRestriction < 0 ||
            SettingsInput.LateRegistrationFee < 0m)
        {
            ModelState.AddModelError(
                string.Empty,
                "Check course limits, carry limits, and late-registration fee.");
        }

        return ModelState.IsValid;
    }

    private async Task LoadPageAsync()
    {
        Settings = await settingsService.GetAsync();

        Schools = await database.Schools
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync();

        Programmes = await database.Programmes
            .Include(item => item.School)
            .AsNoTracking()
            .OrderBy(item => item.Code)
            .ToListAsync();

        StudyModes = await database.StudyModes
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync();

        Courses = await database.Courses
            .Include(item => item.Programme)
            .Include(item => item.StudyMode)
            .AsNoTracking()
            .OrderBy(item => item.Code)
            .ToListAsync();

        Prerequisites = await database.CoursePrerequisites
            .Include(item => item.Course)
            .Include(item => item.PrerequisiteCourse)
            .AsNoTracking()
            .OrderBy(item => item.Course.Code)
            .ThenBy(item => item.PrerequisiteCourse.Code)
            .ToListAsync();

        Lecturers = await database.Lecturers
            .AsNoTracking()
            .OrderBy(item => item.FullName)
            .ToListAsync();

        Rooms = await database.Rooms
            .AsNoTracking()
            .OrderBy(item => item.Code)
            .ToListAsync();
    }

    private static AcademicSettingsInput ToInput(AcademicSettings settings) =>
        new()
        {
            AcademicYear = settings.AcademicYear,
            Semester = settings.Semester,
            IsRegistrationOpen = settings.IsRegistrationOpen,
            IsLateRegistrationOpen = settings.IsLateRegistrationOpen,
            RegistrationPaymentPercentage =
                settings.RegistrationPaymentPercentage,
            ExamSlipPaymentPercentage =
                settings.ExamSlipPaymentPercentage,
            ResultsPaymentPercentage =
                settings.ResultsPaymentPercentage,
            MaximumTotalCourses = settings.MaximumTotalCourses,
            MaximumCarryCourses = settings.MaximumCarryCourses,
            FailedCoursesForRestriction =
                settings.FailedCoursesForRestriction,
            LateRegistrationFee = settings.LateRegistrationFee
        };

    public sealed class AcademicSettingsInput
    {
        public string AcademicYear { get; set; } = string.Empty;
        public string Semester { get; set; } = string.Empty;
        public bool IsRegistrationOpen { get; set; }
        public bool IsLateRegistrationOpen { get; set; }
        public decimal RegistrationPaymentPercentage { get; set; }
        public decimal ExamSlipPaymentPercentage { get; set; }
        public decimal ResultsPaymentPercentage { get; set; }
        public int MaximumTotalCourses { get; set; }
        public int MaximumCarryCourses { get; set; }
        public int FailedCoursesForRestriction { get; set; }
        public decimal LateRegistrationFee { get; set; }
    }

    public sealed class SchoolInput
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public sealed class ProgrammeInput
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal TuitionAmount { get; set; }
        public int SchoolId { get; set; }
    }

    public sealed class StudyModeInput
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int MaximumCourses { get; set; } = 4;
        public decimal AdditionalFee { get; set; }
        public string PaymentAccountName { get; set; } = string.Empty;
        public string PaymentAccountReference { get; set; } = string.Empty;
    }

    public sealed class CourseInput
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int ProgrammeId { get; set; }
        public int StudyModeId { get; set; }
        public int CreditUnits { get; set; } = 4;
        public int YearLevel { get; set; } = 1;
        public string SemesterOffered { get; set; } = "Semester 2";
        public int ExpectedEnrollment { get; set; } = 40;
        public string CourseType { get; set; } = "Core";
    }

    public sealed class PrerequisiteInput
    {
        public int CourseId { get; set; }
        public int PrerequisiteCourseId { get; set; }
    }

    public sealed class LecturerInput
    {
        public string StaffNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Specialisation { get; set; } = string.Empty;
        public string AvailableDays { get; set; } = string.Empty;
    }

    public sealed class RoomInput
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public string RoomType { get; set; } = string.Empty;
    }
}