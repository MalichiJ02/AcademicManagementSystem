# Intelligent Academic Management System with Timetable Optimization

A CS301 group-project prototype for Copperbelt University, Lusaka Campus. It improves selected academic workflows without replacing the existing CBU portal, payment process, or institutional database.

## What changed in this version

This version starts with an empty academic database. It does **not** create fictional students, invoices, courses, payments, or timetable entries automatically.

The first user creates an administrator account, then the administrator configures:

- Programmes and course fees
- Full-time, Part-time, and Evening study modes
- Study-mode payment accounts and maximum course loads
- Academic year, semester, payment thresholds, late-registration fee, and progression limits
- Courses, multiple prerequisites, lecturers, rooms, and student accounts

Students then sign in to their own accounts and generate their own invoices, view payment history, self-register, view their timetable and attendance, check exam-slip eligibility, and access published results when financially cleared.

## Rules implemented

| Rule | Implementation |
|---|---|
| Registration payment threshold | Administrator-configurable; defaults to 30% |
| Exam-slip threshold | Administrator-configurable; defaults to 80% plus confirmed registration |
| Results threshold | Administrator-configurable; defaults to 100% plus published result |
| Normal registration limit | Maximum 4 total courses by default, including carries |
| Carry-course limit | Maximum 2 carries during normal processing by default |
| Progression restriction | More than 2 failed courses in a semester restricts the student to failed courses only |
| Prerequisites | Every configured prerequisite must have a recorded pass |
| Timetable | Checks capacity, room, lecturer, ordinary student, and carry-course conflicts |

## Technology

- ASP.NET Core Razor Pages
- C# / .NET 10
- ASP.NET Core Identity
- Entity Framework Core 10
- SQLite
- Bootstrap and custom CSS

## Run locally

Install the .NET 10 SDK. From this repository, run:

    dotnet restore AcademicManagementSystem.slnx
    dotnet run --project src/AcademicManagementSystem.Web

Open [http://127.0.0.1:5100](http://127.0.0.1:5100).

On the first run:

1. Create the first administrator account at /Setup.
2. Open **Academic setup** and enter the academic period and rules.
3. Add a programme and the three study modes: Full-time, Part-time, and Evening.
4. Add courses, prerequisites, lecturers, and rooms.
5. Create student records and secure student accounts.
6. Publish timetable entries from the Coordinator workspace.
7. Students can then sign in, generate invoices, and self-register.

Two local databases are created in src/AcademicManagementSystem.Web/App_Data/:

- academic-management-v2.db — academic records
- academic-identity-v2.db — secure user accounts and roles

They are intentionally excluded from the ZIP and source control so every new copy starts empty.

## Roles

| Role | Main responsibility |
|---|---|
| Administrator | Configures academic data, rules, students, accounts, and payment records. |
| Student | Generates own invoice, views own status, self-registers, and views own academic information. |
| Coordinator | Manages exceptions, timetable allocation, attendance, monitoring, result entry, and publication. |

## Safe scope boundary

The system never processes a real payment and does not connect to the CBU portal, Zanaco Bill Muster, or real student data. Payment records are local prototype records entered after an administrator confirms information.

## Documentation

- [Architecture and module guide](docs/architecture.md)
- [Requirements traceability](docs/requirements-traceability.md)
- [Gap review and future improvements](docs/gap-review.md)
- [Team learning guide](docs/team-learning-guide.md)
- [Chapter 5 implementation notes](docs/chapter-5-implementation-notes.md)
- [Chapter 6 testing evidence](docs/chapter-6-test-results.md)
- [Presentation demo script](docs/demo-script.md)
