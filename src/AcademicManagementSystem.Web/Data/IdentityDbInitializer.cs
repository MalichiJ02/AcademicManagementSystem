using System.Data;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Data;

/// <summary>
/// Safely upgrades the Identity SQLite database without deleting staff or
/// student login accounts.
/// </summary>
public static class IdentityDbInitializer
{
    public static async Task InitialiseAsync(
        ApplicationIdentityDbContext database)
    {
        await database.Database.EnsureCreatedAsync();

        var connection = database.Database.GetDbConnection();
        var closeConnectionWhenFinished =
            connection.State != ConnectionState.Open;

        if (closeConnectionWhenFinished)
        {
            await connection.OpenAsync();
        }

        try
        {
            if (!await ColumnExistsAsync(
                    connection,
                    "AspNetUsers",
                    "CoordinatingSchoolId"))
            {
                await ExecuteAsync(
                    connection,
                    """
                    ALTER TABLE "AspNetUsers"
                    ADD COLUMN "CoordinatingSchoolId" INTEGER NULL;
                    """);
            }

            await ExecuteAsync(
                connection,
                """
                CREATE INDEX IF NOT EXISTS "IX_AspNetUsers_CoordinatingSchoolId"
                ON "AspNetUsers" ("CoordinatingSchoolId");
                """);
        }
        finally
        {
            if (closeConnectionWhenFinished)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> ColumnExistsAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            if (reader.GetString(1).Equals(
                    columnName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task ExecuteAsync(
        System.Data.Common.DbConnection connection,
        string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}