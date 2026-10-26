using Microsoft.AspNetCore.Identity;

namespace AcademicManagementSystem.Web.Models;

/// <summary>
/// Authentication account. Student academic data remains in the academic
/// database and is connected with Student.IdentityUserId.
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public int? StudentId { get; set; }
    public bool IsActive { get; set; } = true;
}
