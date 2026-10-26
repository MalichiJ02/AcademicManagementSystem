namespace AcademicManagementSystem.Web.Data;

/// <summary>
/// Deliberately does not seed students, courses, invoices, payments, or
/// timetable records. The administrator creates the system's data after the
/// first secure sign-in.
/// </summary>
public static class DbInitializer
{
    public static Task InitialiseAsync(AcademicManagementDbContext database) =>
        database.Database.EnsureCreatedAsync();
}
