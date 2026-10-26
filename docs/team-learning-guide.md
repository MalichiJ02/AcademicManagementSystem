# Team Learning Guide

Do not try to understand every file at once. Run the system, create a small fictional academic setup, follow one request from browser to database, then take one module each.

## Start the system

    dotnet restore AcademicManagementSystem.slnx
    dotnet run --project src/AcademicManagementSystem.Web

Open the local address printed in the terminal, normally http://127.0.0.1:5100. The first page is Setup because the two local SQLite databases start without students, courses, invoices, payments, or timetables.

To reset only local prototype data, stop the application and delete the two version-two database files in src/AcademicManagementSystem.Web/App_Data. Do not delete source files or the old demonstration database unless the team intentionally decides to remove it.

## Learn one complete request flow

Use student self-registration as the first example:

1. An administrator creates a programme, study mode, course, timetable entry, Student record, and linked Student account.
2. The Student signs in at Account/Login and generates an invoice on Student/Invoices.
3. The administrator records a fictional payment on Admin/Finance.
4. The Student selects eligible courses on Student/Registration.
5. RegistrationService checks financial clearance, progression, prerequisites, course limits, course offering, and timetable clashes.
6. Entity Framework saves only confirmed CourseRegistration records in SQLite.
7. The Student dashboard reads those records and displays the outcome.

## Suggested team ownership for learning

| Team member | Start with | First thing to explain to the team |
|---|---|---|
| Malichi | Program.cs, Data, and Services | How the empty databases, Identity roles, and reusable rules work. |
| Natasha | Pages, Pages/Admin, Pages/Student, Pages/Coordinator, and CSS | How each role sees only the correct workflow and feedback. |
| Webby | Chapter 5 notes and registration interface | How technologies, modules, database entities, and interfaces are documented. |
| Wiseman | Chapter 6 evidence and the demo script | How to repeat test cases, record results, and report defects. |

## First team meeting exercise

Take 45 minutes:

1. Everyone runs the app locally.
2. Malichi creates a small fictional programme and the three study modes.
3. Natasha creates one Student account and demonstrates invoice generation and self-registration.
4. Wiseman repeats the 30%, 80%, and 100% financial-gate checks and logs each outcome.
5. Webby captures labelled screenshots of Setup, Academic setup, Invoices, Registration, and Timetable for Chapters 5 and 6.

## What to say when asked how the system works

The prototype is an ASP.NET Core Razor Pages application. ASP.NET Core Identity controls Administrator, Student, and Coordinator access. Entity Framework Core stores prototype academic data in SQLite. Razor Pages accept actions, services apply academic and financial rules, and the coordinator tools use the same controlled data to manage timetables, attendance, monitoring, and result publication.
