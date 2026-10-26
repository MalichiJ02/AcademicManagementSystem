using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Services;

public sealed class AcademicSettingsService(AcademicManagementDbContext database)
{
    public async Task<AcademicSettings> GetAsync()
    {
        var settings = await database.AcademicSettings
            .SingleOrDefaultAsync(item => item.Id == AcademicSettings.SingletonId);

        if (settings is not null)
        {
            return settings;
        }

        settings = new AcademicSettings();
        database.AcademicSettings.Add(settings);
        await database.SaveChangesAsync();
        return settings;
    }
}
