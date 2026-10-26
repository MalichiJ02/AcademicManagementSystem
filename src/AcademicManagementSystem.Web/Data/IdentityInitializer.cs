using Microsoft.AspNetCore.Identity;

namespace AcademicManagementSystem.Web.Data;

public static class IdentityInitializer
{
    public static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = ["Administrator", "Student", "Coordinator"];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not create the {role} role: " +
                        string.Join("; ", result.Errors.Select(error => error.Description)));
                }
            }
        }
    }
}
