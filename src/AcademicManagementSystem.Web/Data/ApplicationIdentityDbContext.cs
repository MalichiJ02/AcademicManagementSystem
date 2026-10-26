using AcademicManagementSystem.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Data;

public sealed class ApplicationIdentityDbContext(
    DbContextOptions<ApplicationIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
}
