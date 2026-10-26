# Chapter 5 — System Implementation

## 5.1 Introduction

The Intelligent Academic Management System with Timetable Optimization was implemented as a web-based prototype. The implementation transforms the earlier demo-led prototype into a role-based, administrator-managed academic workflow. The database begins without fictional student records. An administrator first creates academic data, then students use secure individual accounts to perform normal self-service activities.

## 5.2 Technologies and tools

| Item | Implementation choice |
|---|---|
| Programming language | C# |
| Runtime and web framework | .NET 10 and ASP.NET Core Razor Pages |
| Authentication | ASP.NET Core Identity with role-based authorization |
| Database access | Entity Framework Core 10 |
| Database | SQLite, using separate academic and identity databases |
| Front end | Razor Views, HTML, Bootstrap, and custom CSS |
| Version control | Git and GitHub |
| Development tools | Visual Studio Code and .NET CLI |

## 5.3 System components

The implementation contains three secured workspaces.

| Workspace | Implemented functions |
|---|---|
| Administrator | Initial setup, users, students, programmes, study modes, courses, prerequisites, rules, rooms, lecturers, invoice review, and payment records. |
| Student | Own dashboard, current and previous invoices, payment history, self-registration, timetable, attendance, exam-slip eligibility, and published results. |
| Coordinator | Academic eligibility exceptions, timetable generation, attendance capture, academic monitoring, result entry, and result publication. |

## 5.4 Authentication and authorization implementation

ASP.NET Core Identity was added through an ApplicationUser class and an ApplicationIdentityDbContext. Three roles are created when the application starts: Administrator, Student, and Coordinator.

The first user is directed to a Setup page, where an administrator account is created. Passwords require a minimum of eight characters with an uppercase letter, lowercase letter, and digit. Public student account creation is not provided. Instead, an administrator creates each Student record and a linked Identity account.

Page-level authorization protects the workspaces. For example, Student pages require the Student role and are linked to the currently signed-in student. A student cannot choose another student from a dropdown or open Administrator pages. A request-time active-account check also signs out a disabled account even if an old browser session remains.

## 5.5 Database implementation

Entity Framework Core Code First defines the academic database. The main entities are Student, Programme, StudyMode, AcademicSettings, Course, CoursePrerequisite, Invoice, PaymentRecord, CourseRegistration, CompletedCourse, StudentResult, Lecturer, Room, TimetableEntry, AttendanceSession, and AttendanceRecord.

The identity database contains the ASP.NET Core Identity tables and ApplicationUser records. Separating account data from academic records makes the role boundary clear in the prototype.

No fictional students, payments, courses, or timetables are seeded. The first administrator creates the values required by the system. The application uses fresh version-two SQLite file names so upgrading from the earlier demo prototype does not silently overwrite its old database.

## 5.6 Academic configuration module

The Administrator Academic setup page implements the configurable parameters below.

| Parameter | Default prototype value |
|---|---|
| Registration payment threshold | 30% |
| Exam-slip payment threshold | 80% |
| Results payment threshold | 100% |
| Maximum total courses | 4 |
| Maximum carry courses during normal processing | 2 |
| Failed courses before restriction | More than 2 |
| Late-registration fee | ZMW 600 |

The administrator also creates Full-time, Part-time, and Evening study modes. Each mode stores a maximum course load, additional fee, payment-account name, and payment reference, all of which can be updated from the setup page. A student invoice is generated from the student programme tuition, study-mode fee, and the late fee only when late registration is enabled.

## 5.7 Student invoice and registration implementation

The student signs in and generates an invoice for the configured academic period. The InvoiceService prevents duplicate invoices for the same student and period. It records the programme and study mode on the invoice so historical invoices remain meaningful if settings are later changed.

Payment is not processed by the prototype. An administrator records a confirmed payment amount and reference as a PaymentRecord. The invoice total, paid amount, percentage, and balance are recalculated for access decisions.

The RegistrationService validates:

- Whether registration is open and the configured payment threshold has been reached.
- Academic eligibility and active student status.
- Programme and study-mode compatibility.
- Year level and semester offering.
- Passed course prerequisites.
- Previously passed courses and duplicate registrations.
- A maximum of four total courses by default.
- A maximum of two carry courses during normal processing.
- A restricted progression case where more than two failures in a semester allows failed courses only.
- Published timetable availability and timetable clashes.

## 5.8 Timetable optimization implementation

The TimetableService validates room capacity, lecturer availability, room clashes, lecturer clashes, study-mode schedule compatibility, and registered-student clashes. It uses the larger of expected enrolment and confirmed registration count for capacity checks. The automatic allocation routine considers only courses offered in the current semester, then iterates through compatible teaching slots, lecturers, and rooms. Evening-only courses use evening slots. It rejects hard constraint failures, then scores remaining candidates. Ordinary student conflicts are prioritised, while carry-course conflicts are assessed last and minimised as a lower-weight penalty.

This is a transparent heuristic suitable for a prototype. It demonstrates optimization decisions clearly but is not presented as a complete operations-research solver.

## 5.9 Attendance, monitoring, and results

The coordinator creates attendance sessions and records Present, Late, or Absent using a student number. Only students with confirmed registration for the session course can be recorded. Students see only their own attendance.

The monitoring service produces Low, Moderate, or High risk indicators from academic holds, attendance percentage, and available result marks.

For results, a coordinator enters a mark only for a student with a confirmed course registration. Publication changes the result state to published and updates the CompletedCourse pass/fail record. The progression status is recalculated. Students can view a published result batch only when the matching invoice meets the configured results-payment threshold.

## 5.10 User interfaces

The user interface uses responsive Razor Pages, Bootstrap, and custom CSS. It includes:

- Role-aware navigation and a sign-out control.
- Setup and login forms with validation feedback.
- Administrator forms for academic setup and student management.
- Student status cards, payment-progress displays, course-selection controls, and locked-access reasons.
- Coordinator dashboards, timetable health reports, attendance rosters, and result-publication controls.

## 5.11 Implementation limitations

- The prototype uses local SQLite databases and Entity Framework EnsureCreated rather than production migrations.
- There is no live CBU portal, payment gateway, bank, or Zanaco Bill Muster integration.
- Payment records are administrator-entered prototype information, not actual transactions.
- Password reset, email confirmation, audit logging, and multi-factor authentication are future production features.
- The timetable algorithm is a constrained heuristic; larger institutional timetables would require stronger optimization and performance testing.
