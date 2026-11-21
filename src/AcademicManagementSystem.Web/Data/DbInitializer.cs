using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Data;

/// <summary>
/// Creates the initial database and safely applies small prototype schema
/// upgrades without deleting administrator-imported academic data.
/// </summary>
public static class DbInitializer
{
    public static async Task InitialiseAsync(AcademicManagementDbContext database)
    {
        await database.Database.EnsureCreatedAsync();
        await ApplyPrototypeSchemaAsync(database);
    }

    private static async Task ApplyPrototypeSchemaAsync(
        AcademicManagementDbContext database)
    {
        var connection = database.Database.GetDbConnection();
        var closeConnectionWhenFinished = connection.State != ConnectionState.Open;

        if (closeConnectionWhenFinished)
        {
            await connection.OpenAsync();
        }

        try
        {
            await ApplySchoolSchemaAsync(connection);
            await ApplyCourseSessionSchemaAsync(connection);
            await ApplyOptionalLecturerSchemaAsync(connection);
        }
        finally
        {
            if (closeConnectionWhenFinished)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task ApplySchoolSchemaAsync(
        System.Data.Common.DbConnection connection)
    {
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS "Schools" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Schools"
                    PRIMARY KEY AUTOINCREMENT,
                "Code" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL
            );
            """);

        await ExecuteAsync(
            connection,
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Schools_Code"
            ON "Schools" ("Code");
            """);

        if (!await ColumnExistsAsync(connection, "Programmes", "SchoolId"))
        {
            await ExecuteAsync(
                connection,
                """
                ALTER TABLE "Programmes"
                ADD COLUMN "SchoolId" INTEGER NULL;
                """);
        }

        await ExecuteAsync(
            connection,
            """
            CREATE INDEX IF NOT EXISTS "IX_Programmes_SchoolId"
            ON "Programmes" ("SchoolId");
            """);
    }

    private static async Task ApplyCourseSessionSchemaAsync(
        System.Data.Common.DbConnection connection)
    {
        // Replace the old unique index, which permitted only one session per term.
        using var indexCheck = connection.CreateCommand();
        indexCheck.CommandText = """
            SELECT COUNT(*) FROM pragma_index_list('TimetableEntries')
            WHERE name = 'IX_TimetableEntries_CourseId_AcademicYear_Semester' AND "unique" = 1;
            """;

        if (Convert.ToInt64(await indexCheck.ExecuteScalarAsync()) != 0)
        {
            await ExecuteAsync(connection, """
                DROP INDEX "IX_TimetableEntries_CourseId_AcademicYear_Semester";
                """);
        }

        await ExecuteAsync(connection, """
            CREATE INDEX IF NOT EXISTS "IX_TimetableEntries_CourseId_AcademicYear_Semester"
            ON "TimetableEntries" ("CourseId", "AcademicYear", "Semester");
            """);

        if (!await ColumnExistsAsync(connection, "Courses", "SessionsPerWeek"))
        {
            await ExecuteAsync(
                connection,
                """
                ALTER TABLE "Courses"
                ADD COLUMN "SessionsPerWeek" INTEGER NOT NULL DEFAULT 2;
                """);
        }

        await ExecuteAsync(
            connection,
            """
            UPDATE "Courses"
            SET "SessionsPerWeek" = 2
            WHERE "SessionsPerWeek" IS NULL
               OR "SessionsPerWeek" < 1;
            """);
    }

    private static async Task ApplyOptionalLecturerSchemaAsync(DbConnection connection)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT "notnull" FROM pragma_table_info('TimetableEntries')
            WHERE name = 'LecturerId';
            """;

        if (Convert.ToInt64(await command.ExecuteScalarAsync()) == 1)
        {
            // SQLite requires a table rebuild to remove the NOT NULL constraint.
            command.CommandText = """
                CREATE TABLE "TimetableEntries_OptionalLecturer" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_TimetableEntries" PRIMARY KEY AUTOINCREMENT,
                    "CourseId" INTEGER NOT NULL,
                    "LecturerId" INTEGER NULL,
                    "RoomId" INTEGER NOT NULL,
                    "AcademicYear" TEXT NOT NULL,
                    "Semester" TEXT NOT NULL,
                    "DayOfWeek" TEXT NOT NULL,
                    "StartMinutes" INTEGER NOT NULL,
                    "EndMinutes" INTEGER NOT NULL,
                    "Status" TEXT NOT NULL,
                    CONSTRAINT "FK_TimetableEntries_Courses_CourseId"
                        FOREIGN KEY ("CourseId") REFERENCES "Courses" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_TimetableEntries_Lecturers_LecturerId"
                        FOREIGN KEY ("LecturerId") REFERENCES "Lecturers" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_TimetableEntries_Rooms_RoomId"
                        FOREIGN KEY ("RoomId") REFERENCES "Rooms" ("Id") ON DELETE RESTRICT
                );

                INSERT INTO "TimetableEntries_OptionalLecturer"
                    ("Id", "CourseId", "LecturerId", "RoomId", "AcademicYear", "Semester",
                     "DayOfWeek", "StartMinutes", "EndMinutes", "Status")
                SELECT "Id", "CourseId", "LecturerId", "RoomId", "AcademicYear", "Semester",
                       "DayOfWeek", "StartMinutes", "EndMinutes", "Status"
                FROM "TimetableEntries";

                UPDATE "sqlite_sequence"
                SET "seq" = MAX("seq", COALESCE(
                    (SELECT "seq" FROM "sqlite_sequence" WHERE "name" = 'TimetableEntries'), 0))
                WHERE "name" = 'TimetableEntries_OptionalLecturer';

                DROP TABLE "TimetableEntries";
                ALTER TABLE "TimetableEntries_OptionalLecturer" RENAME TO "TimetableEntries";

                CREATE INDEX "IX_TimetableEntries_CourseId_AcademicYear_Semester"
                    ON "TimetableEntries" ("CourseId", "AcademicYear", "Semester");
                CREATE INDEX "IX_TimetableEntries_LecturerId" ON "TimetableEntries" ("LecturerId");
                CREATE INDEX "IX_TimetableEntries_RoomId" ON "TimetableEntries" ("RoomId");
                """;
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
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