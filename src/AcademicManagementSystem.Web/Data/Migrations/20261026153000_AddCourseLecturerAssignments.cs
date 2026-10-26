using AcademicManagementSystem.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AcademicManagementSystem.Web.Data.Migrations;

[DbContext(typeof(AcademicManagementDbContext))]
[Migration("20261026153000_AddCourseLecturerAssignments")]
public partial class AddCourseLecturerAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CourseLecturerAssignments",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),

                CourseId = table.Column<int>(nullable: false),

                LecturerId = table.Column<int>(nullable: false),

                IsPrimary = table.Column<bool>(nullable: false),

                IsActive = table.Column<bool>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_CourseLecturerAssignments",
                    assignment => assignment.Id);

                table.ForeignKey(
                    name: "FK_CourseLecturerAssignments_Courses_CourseId",
                    column: assignment => assignment.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_CourseLecturerAssignments_Lecturers_LecturerId",
                    column: assignment => assignment.LecturerId,
                    principalTable: "Lecturers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CourseLecturerAssignments_CourseId_LecturerId",
            table: "CourseLecturerAssignments",
            columns: new[] { "CourseId", "LecturerId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CourseLecturerAssignments");
    }
}