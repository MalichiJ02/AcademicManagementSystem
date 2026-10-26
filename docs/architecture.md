# Prototype Architecture

## Purpose

The system models selected academic-management workflows for the Intelligent Academic Management System with Timetable Optimization project. It improves information handling and decision support; it does not replace the CBU portal or process payments.

## Architecture overview

| Layer | Main responsibility |
|---|---|
| Presentation | Razor Pages supply separate Administrator, Student, and Coordinator workspaces. |
| Authentication | ASP.NET Core Identity stores secure user accounts and roles in a separate SQLite identity database. |
| Application services | Reusable services apply financial access, invoice, registration, progression, attendance, risk, and timetable rules. |
| Academic data | Entity Framework Core maps administrator-managed academic data to a SQLite database. |
| Styling | Bootstrap and custom CSS give responsive dashboards, forms, status indicators, and validation feedback. |

## Secure role flow

1. A new installation opens the Setup page because no administrator account exists.
2. The first administrator account is created with ASP.NET Core Identity.
3. Identity roles are Administrator, Student, and Coordinator.
4. Administrators create student records and linked student accounts.
5. A signed-in student is resolved through the linked academic Student record, not through a student selector.
6. Razor Page authorization prevents users from opening pages outside their role.
7. A request-time active-account check signs out disabled accounts before protected content is served.

## Main data entities

| Entity | Purpose |
|---|---|
| ApplicationUser | Secure account, role membership, active status, and optional linked student ID. |
| Student | Student number, programme, study mode, year, eligibility, progression status, and account link. |
| Programme | Programme code, name, and base tuition amount. |
| StudyMode | Full-time, Part-time, or Evening rules, additional fee, payment account, and maximum course load. |
| AcademicSettings | Current academic period, payment thresholds, late fee, and course/progression limits. |
| Course and CoursePrerequisite | Courses, semester offerings, programme/mode assignment, and one or more prerequisites. |
| Invoice and PaymentRecord | Student-generated invoices plus administrator-recorded payment history. |
| CourseRegistration and CompletedCourse | Confirmed normal/carry registrations and pass/fail history. |
| StudentResult | Mark, grade, academic period, and publication state. |
| Lecturer, Room, TimetableEntry | Timetable resources, availability, capacity, and allocations. |
| AttendanceSession and AttendanceRecord | Teaching sessions and student-ID attendance entries. |

## Rule sequence for self-registration

1. The student signs in and generates an invoice for the selected academic period.
2. The student must meet the administrator-configured registration-payment threshold, 30% by default.
3. The system verifies academic eligibility and registration-open status.
4. It evaluates failed-course progression restrictions and carry-course limits.
5. It checks programme, study mode, year level, semester offering, passed prerequisites, duplicate selections, and the maximum course count.
6. It checks published timetable entries for selected-course and existing-registration conflicts.
7. It saves confirmed CourseRegistration records only when all checks pass.

## Timetable approach

Manual timetable allocation treats room capacity, lecturer availability, room clashes, lecturer clashes, and registered-student clashes as validation checks.

The automatic allocator uses a transparent heuristic:

1. Consider active courses offered in the current semester and use an Evening teaching period for an Evening-only course.
2. Reject impossible candidates such as insufficient capacity, unavailable lecturers, double-booked rooms, or double-booked lecturers.
3. Score remaining candidates by ordinary registered-student conflicts.
4. Check carry-course conflicts last and use them as a lower-weight penalty.
5. Select the best available candidate, preferring a zero-conflict allocation.

This approach actively reduces carry-course conflicts while correctly prioritising core timetable constraints.

## Database startup behaviour

The two SQLite files are created on first run. No student, course, invoice, payment, result, room, lecturer, attendance, or timetable data is seeded automatically. Roles and the initial administrator account are the only setup prerequisites.

The prototype uses Entity Framework EnsureCreated for a simple demonstration database. A future production implementation should replace this with migrations, backups, and institutional database governance.

## Scope boundary

- No live CBU, bank, or Zanaco Bill Muster integration.
- No real financial transaction processing.
- No real student data.
- No public student-account registration.
- No claim that the greedy timetable heuristic is a full mathematical optimum.
