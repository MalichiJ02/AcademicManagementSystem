# Chapter 6 — Evaluation and Testing

## 6.1 Test approach

Testing combined build verification, manual black-box workflow tests, page-access checks, and rule-boundary tests. Tests used temporary administrator-created prototype records. Those records were removed after verification so the delivered system starts with an empty database.

## 6.2 Test environment

| Item | Value |
|---|---|
| Application | ASP.NET Core Razor Pages prototype |
| Runtime | .NET 10 SDK |
| Database | Local SQLite academic database and separate Identity database |
| Test browser/client | Local HTTP requests and browser-ready Razor Pages |
| Data approach | Temporary administrator-created test records; no real student or payment data |
| Build outcome | Successful build with 0 warnings and 0 errors |

## 6.3 Test cases and results

| ID | Test case | Expected result | Actual result | Status |
|---|---|---|---|---|
| T01 | Start the application with no databases. | The application must show first-time administrator setup and no fictional records. | Root route redirected to Setup; Administrator dashboard reported zero students and zero timetable entries. | Pass |
| T02 | Create the first Administrator account. | Secure account is created and directed to the Administrator workspace. | Setup redirected to Administrator dashboard after valid password creation. | Pass |
| T03 | Open Student registration anonymously. | Unauthorized user is redirected to sign in. | Route redirected to Account Login with a return URL. | Pass |
| T04 | Sign in as a Student and try opening Administrator area. | Student cannot access Administrator pages. | Route redirected to Access Denied. | Pass |
| T05 | Add programme, Full-time study mode, course, lecturer, and room. | Administrator-managed academic data is saved. | Each form saved successfully and data appeared in setup lists. | Pass |
| T06 | Generate timetable allocation for the active course. | A valid allocation is created using lecturer availability and room capacity. | Allocation was generated and student timetable displayed Monday 09:00–11:00. | Pass |
| T07 | Create a Student record and linked secure account. | Student can sign in only to their own portal. | Student account was created and routed to Student dashboard. | Pass |
| T08 | Student generates current invoice. | Invoice uses programme fee and study-mode payment account. | Invoice total and stated Full-time payment account were displayed. | Pass |
| T09 | Record 30% payment against the student invoice. | Registration becomes available at configured threshold. | Dashboard and registration page showed 30% paid and registration available. | Pass |
| T10 | Student submits a configured course registration. | Confirmed registration is saved after validation. | One confirmed registration was saved; duplicate selection became unavailable. | Pass |
| T11 | Student opens exam-slip page at 30% payment. | Exam slip remains locked until 80% and confirmed registration. | Page showed locked state and 80% payment reason. | Pass |
| T12 | Coordinator enters and publishes a result at 30% payment. | Published result exists but remains hidden from student until 100%. | Student result batch displayed as financially locked. | Pass |
| T13 | Record remaining payment to reach 100%. | Published result becomes visible. | Result batch became available and displayed the mark and grade. | Pass |
| T14 | Open all secured Administrator, Coordinator, Student, and static CSS routes. | Pages render without server errors. | All tested pages and CSS returned HTTP 200. | Pass |
| T15 | Build after implementation changes. | No compile errors or warnings. | Build completed with 0 warnings and 0 errors. | Pass |
| T16 | Update an existing Evening study mode. | Administrator can change its description, fee, and payment account. | The updated description, account name, and reference appeared on Academic setup. | Pass |
| T17 | Allocate an Evening-only course automatically. | The course must be assigned an evening teaching period. | EV101 was allocated Thursday 17:00–19:00 and the constraint report passed. | Pass |
| T18 | Disable a signed-in Coordinator account. | An old Coordinator session must no longer reach protected pages. | The next Coordinator request was redirected to Account Login. | Pass |

## 6.4 Evaluation against objectives

| Objective | Evaluation result |
|---|---|
| Secure account separation | Implemented through ASP.NET Core Identity with Administrator, Student, and Coordinator roles. |
| Empty administrator-managed database | Implemented; no fictional student, payment, or timetable records are seeded. |
| Student self-registration | Implemented with student-specific login, invoice generation, financial clearance, prerequisites, progression, course-limit, and timetable checks. |
| Financial access control | Implemented with configurable 30%, 80%, and 100% thresholds. |
| Study-mode support | Implemented with Full-time, Part-time, and Evening administrator-created modes, payment accounts, and course limits. |
| Academic progression and carries | Implemented with failed-course history, two-carry normal limit, and restricted progression after more than two failures. |
| Timetable optimization | Implemented with capacity, availability, room, lecturer, student, and carry-course conflict handling. |
| Active-account enforcement | Implemented; disabled accounts are signed out before protected content is served. |
| Documentation support | README, Chapter 5, Chapter 6, traceability, gap review, and demo script updated with the new workflow. |

## 6.5 Functionality and usability observations

- First-time setup is clear because the application redirects to the initial Administrator account page.
- Students do not select another student from a demo dropdown; their account determines the data they can see.
- Locked actions explain the specific reason, such as missing invoice, insufficient payment, unpublished result, or failed prerequisite.
- Administrator forms keep the data-entry order visible: settings, programmes/study modes, courses/resources, students, then financial records.
- The responsive interface uses clear status pills, tables, forms, and progress displays.

## 6.6 Reliability and limitations

The prototype successfully handled the tested one-student workflow and all routed pages. Its limits are appropriate to the project stage:

- It has not been load tested with institutional-scale numbers of students, courses, and timetable entries.
- The scheduling method is a transparent greedy heuristic, not a full optimal solver.
- SQLite and EnsureCreated are appropriate for the prototype but not an institutional deployment.
- Financial information is local prototype data; no payment is initiated, verified, or received from a bank.
- Production rollout would require data migration, audit trails, institutional authorization, password-reset and email workflows, API security, backups, and privacy controls.

## 6.7 Defect discovered and corrected during testing

During route testing, the Student timetable page initially attempted to sort a database query using a local DayOrder method. Entity Framework cannot translate that local method to SQL, which caused a server error. The query was corrected by retrieving entries first and applying the day-order sort in memory. The route was then retested successfully with HTTP 200.
