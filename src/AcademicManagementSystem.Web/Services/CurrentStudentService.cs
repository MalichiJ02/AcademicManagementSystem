using System.Security.Claims;
using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Services;

public sealed class CurrentStudentService(
    UserManager<ApplicationUser> userManager,
    AcademicManagementDbContext database)
{
    public async Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal) =>
        await userManager.GetUserAsync(principal);

    public async Task<Student?> GetStudentAsync(ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return null;
        }

        return await database.Students
            .Include(student => student.ProgrammeRecord)
            .Include(student => student.StudyModeRecord)
            .SingleOrDefaultAsync(student =>
                student.IdentityUserId == user.Id ||
                (user.StudentId.HasValue && student.Id == user.StudentId.Value));
    }
}
