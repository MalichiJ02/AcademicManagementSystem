using AcademicManagementSystem.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AcademicManagementSystem.Web.Data;

public sealed class AcademicManagementDbContext(
    DbContextOptions<AcademicManagementDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Programme> Programmes => Set<Programme>();
    public DbSet<StudyMode> StudyModes => Set<StudyMode>();
    public DbSet<AcademicSettings> AcademicSettings => Set<AcademicSettings>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CoursePrerequisite> CoursePrerequisites => Set<CoursePrerequisite>();
    public DbSet<CourseLecturerAssignment> CourseLecturerAssignments
    => Set<CourseLecturerAssignment>();
    public DbSet<CourseRegistration> CourseRegistrations => Set<CourseRegistration>();
    public DbSet<CompletedCourse> CompletedCourses => Set<CompletedCourse>();
    public DbSet<StudentResult> StudentResults => Set<StudentResult>();
    public DbSet<Lecturer> Lecturers => Set<Lecturer>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<TimetableEntry> TimetableEntries => Set<TimetableEntry>();
    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasIndex(student => student.StudentNumber).IsUnique();
            entity.HasIndex(student => student.IdentityUserId).IsUnique();
            entity.Property(student => student.StudentNumber).HasMaxLength(30).IsRequired();
            entity.Property(student => student.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(student => student.LastName).HasMaxLength(80).IsRequired();
            entity.Property(student => student.Programme).HasMaxLength(160).IsRequired();
            entity.Property(student => student.StudyMode).HasMaxLength(60).IsRequired();
            entity.Property(student => student.ProgressionStatus).HasMaxLength(40).IsRequired();
            entity.Property(student => student.AcademicHoldReason).HasMaxLength(300);
            entity.Property(student => student.ProgressionReason).HasMaxLength(300);

            entity.HasOne(student => student.ProgrammeRecord)
                .WithMany(programme => programme.Students)
                .HasForeignKey(student => student.ProgrammeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(student => student.StudyModeRecord)
                .WithMany(studyMode => studyMode.Students)
                .HasForeignKey(student => student.StudyModeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Programme>(entity =>
        {
            entity.HasIndex(programme => programme.Code).IsUnique();
            entity.Property(programme => programme.Code).HasMaxLength(30).IsRequired();
            entity.Property(programme => programme.Name).HasMaxLength(160).IsRequired();
            entity.Property(programme => programme.TuitionAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<StudyMode>(entity =>
        {
            entity.HasIndex(studyMode => studyMode.Name).IsUnique();
            entity.Property(studyMode => studyMode.Name).HasMaxLength(60).IsRequired();
            entity.Property(studyMode => studyMode.Description).HasMaxLength(300).IsRequired();
            entity.Property(studyMode => studyMode.AdditionalFee).HasPrecision(18, 2);
            entity.Property(studyMode => studyMode.PaymentAccountName).HasMaxLength(160).IsRequired();
            entity.Property(studyMode => studyMode.PaymentAccountReference).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<AcademicSettings>(entity =>
        {
            entity.HasKey(settings => settings.Id);
            entity.Property(settings => settings.AcademicYear).HasMaxLength(20).IsRequired();
            entity.Property(settings => settings.Semester).HasMaxLength(40).IsRequired();
            entity.Property(settings => settings.RegistrationPaymentPercentage).HasPrecision(5, 2);
            entity.Property(settings => settings.ExamSlipPaymentPercentage).HasPrecision(5, 2);
            entity.Property(settings => settings.ResultsPaymentPercentage).HasPrecision(5, 2);
            entity.Property(settings => settings.LateRegistrationFee).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasIndex(invoice => invoice.InvoiceNumber).IsUnique();
            entity.Property(invoice => invoice.TotalAmount).HasPrecision(18, 2);
            entity.Property(invoice => invoice.AmountPaid).HasPrecision(18, 2);
            entity.Property(invoice => invoice.LateRegistrationFee).HasPrecision(18, 2);
            entity.Property(invoice => invoice.InvoiceNumber).HasMaxLength(50).IsRequired();
            entity.Property(invoice => invoice.ProgrammeName).HasMaxLength(160).IsRequired();
            entity.Property(invoice => invoice.StudyModeName).HasMaxLength(60).IsRequired();
            entity.Property(invoice => invoice.PaymentSource).HasMaxLength(200).IsRequired();
            entity.Property(invoice => invoice.PaymentAccount).HasMaxLength(200).IsRequired();
            entity.Property(invoice => invoice.Status).HasMaxLength(30).IsRequired();
            entity.HasOne(invoice => invoice.Student)
                .WithMany(student => student.Invoices)
                .HasForeignKey(invoice => invoice.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.Reference).HasMaxLength(100).IsRequired();
            entity.Property(payment => payment.PaymentAccount).HasMaxLength(200).IsRequired();
            entity.Property(payment => payment.Note).HasMaxLength(300);
            entity.HasOne(payment => payment.Invoice)
                .WithMany(invoice => invoice.Payments)
                .HasForeignKey(payment => payment.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasIndex(course => course.Code).IsUnique();
            entity.Property(course => course.Code).HasMaxLength(20).IsRequired();
            entity.Property(course => course.Title).HasMaxLength(160).IsRequired();
            entity.Property(course => course.SemesterOffered).HasMaxLength(40).IsRequired();
            entity.Property(course => course.ScheduleKey).HasMaxLength(80).IsRequired();
            entity.Property(course => course.Venue).HasMaxLength(80).IsRequired();
            entity.Property(course => course.CourseType).HasMaxLength(30).IsRequired();

            entity.HasOne(course => course.Programme)
                .WithMany(programme => programme.Courses)
                .HasForeignKey(course => course.ProgrammeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(course => course.StudyMode)
                .WithMany(studyMode => studyMode.Courses)
                .HasForeignKey(course => course.StudyModeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CoursePrerequisite>(entity =>
        {
            entity.HasKey(prerequisite => new
            {
                prerequisite.CourseId,
                prerequisite.PrerequisiteCourseId
            });

            entity.HasOne(prerequisite => prerequisite.Course)
                .WithMany(course => course.Prerequisites)
                .HasForeignKey(prerequisite => prerequisite.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(prerequisite => prerequisite.PrerequisiteCourse)
                .WithMany(course => course.RequiredFor)
                .HasForeignKey(prerequisite => prerequisite.PrerequisiteCourseId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseLecturerAssignment>(entity =>
{
    entity.ToTable("CourseLecturerAssignments");

    entity.HasIndex(assignment => new
    {
        assignment.CourseId,
        assignment.LecturerId
    }).IsUnique();

    entity.HasOne(assignment => assignment.Course)
        .WithMany(course => course.LecturerAssignments)
        .HasForeignKey(assignment => assignment.CourseId)
        .OnDelete(DeleteBehavior.Restrict);

    entity.HasOne(assignment => assignment.Lecturer)
        .WithMany(lecturer => lecturer.CourseAssignments)
        .HasForeignKey(assignment => assignment.LecturerId)
        .OnDelete(DeleteBehavior.Restrict);
});

        modelBuilder.Entity<CourseRegistration>(entity =>
        {
            entity.HasIndex(registration => new
            {
                registration.StudentId,
                registration.CourseId,
                registration.AcademicYear,
                registration.Semester
            }).IsUnique();

            entity.HasOne(registration => registration.Student)
                .WithMany(student => student.Registrations)
                .HasForeignKey(registration => registration.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(registration => registration.Course)
                .WithMany(course => course.Registrations)
                .HasForeignKey(registration => registration.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(registration => registration.Status).HasMaxLength(30).IsRequired();
            entity.Property(registration => registration.RegistrationType).HasMaxLength(30).IsRequired();
        });

        modelBuilder.Entity<CompletedCourse>(entity =>
        {
            entity.HasIndex(completed => new { completed.StudentId, completed.CourseCode }).IsUnique();
            entity.HasOne(completed => completed.Student)
                .WithMany(student => student.CompletedCourses)
                .HasForeignKey(completed => completed.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(completed => completed.Course)
                .WithMany()
                .HasForeignKey(completed => completed.CourseId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.Property(completed => completed.AcademicYear).HasMaxLength(20);
            entity.Property(completed => completed.Semester).HasMaxLength(40);
        });

        modelBuilder.Entity<StudentResult>(entity =>
        {
            entity.HasIndex(result => new
            {
                result.StudentId,
                result.CourseId,
                result.AcademicYear,
                result.Semester
            }).IsUnique();
            entity.Property(result => result.Mark).HasPrecision(5, 2);
            entity.Property(result => result.CourseCode).HasMaxLength(20).IsRequired();
            entity.Property(result => result.Grade).HasMaxLength(10).IsRequired();
            entity.HasOne(result => result.Student)
                .WithMany(student => student.Results)
                .HasForeignKey(result => result.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(result => result.Course)
                .WithMany()
                .HasForeignKey(result => result.CourseId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Lecturer>(entity =>
        {
            entity.HasIndex(lecturer => lecturer.StaffNumber).IsUnique();
            entity.Property(lecturer => lecturer.StaffNumber).HasMaxLength(30).IsRequired();
            entity.Property(lecturer => lecturer.FullName).HasMaxLength(120).IsRequired();
            entity.Property(lecturer => lecturer.Specialisation).HasMaxLength(160).IsRequired();
            entity.Property(lecturer => lecturer.AvailableDays).HasMaxLength(160).IsRequired();
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasIndex(room => room.Code).IsUnique();
            entity.Property(room => room.Code).HasMaxLength(30).IsRequired();
            entity.Property(room => room.Name).HasMaxLength(120).IsRequired();
            entity.Property(room => room.RoomType).HasMaxLength(60).IsRequired();
        });

        modelBuilder.Entity<TimetableEntry>(entity =>
        {
            entity.HasIndex(entry => new
            {
                entry.CourseId,
                entry.AcademicYear,
                entry.Semester
            }).IsUnique();

            entity.Property(entry => entry.AcademicYear).HasMaxLength(20).IsRequired();
            entity.Property(entry => entry.Semester).HasMaxLength(40).IsRequired();
            entity.Property(entry => entry.DayOfWeek).HasMaxLength(20).IsRequired();
            entity.Property(entry => entry.Status).HasMaxLength(30).IsRequired();

            entity.HasOne(entry => entry.Course)
                .WithMany(course => course.TimetableEntries)
                .HasForeignKey(entry => entry.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(entry => entry.Lecturer)
                .WithMany(lecturer => lecturer.TimetableEntries)
                .HasForeignKey(entry => entry.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(entry => entry.Room)
                .WithMany(room => room.TimetableEntries)
                .HasForeignKey(entry => entry.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttendanceSession>(entity =>
        {
            entity.Property(session => session.Topic).HasMaxLength(180).IsRequired();
            entity.HasOne(session => session.Course)
                .WithMany(course => course.AttendanceSessions)
                .HasForeignKey(session => session.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasIndex(record => new
            {
                record.AttendanceSessionId,
                record.StudentId
            }).IsUnique();

            entity.Property(record => record.Status).HasMaxLength(20).IsRequired();
            entity.HasOne(record => record.AttendanceSession)
                .WithMany(session => session.Records)
                .HasForeignKey(record => record.AttendanceSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(record => record.Student)
                .WithMany(student => student.AttendanceRecords)
                .HasForeignKey(record => record.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
