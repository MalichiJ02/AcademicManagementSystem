using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Pages.Coordinator;

[Authorize(Roles = "Coordinator,Administrator")]
public class MonitoringModel(
    AcademicManagementDbContext database,
    AcademicRiskService riskService) : PageModel
{
    public IReadOnlyList<AcademicRiskAssessment> Assessments { get; private set; } = [];

    public int HighRiskCount => Assessments.Count(assessment => assessment.Level == "High");
    public int ModerateRiskCount => Assessments.Count(assessment => assessment.Level == "Moderate");
    public int LowRiskCount => Assessments.Count(assessment => assessment.Level == "Low");

    public async Task OnGetAsync()
    {
        var students = await database.Students
            .Include(student => student.Results)
            .Include(student => student.AttendanceRecords)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();

        Assessments = students
            .Select(riskService.Assess)
            .OrderByDescending(assessment => RiskRank(assessment.Level))
            .ThenBy(assessment => assessment.StudentNumber)
            .ToList();
    }

    private static int RiskRank(string level) => level switch
    {
        "High" => 3,
        "Moderate" => 2,
        _ => 1
    };
}
