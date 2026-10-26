# Gap Review and Future Improvements

## Addressed gaps in this version

| Previous gap | Improvement delivered |
|---|---|
| Fictional records appeared automatically | Replaced with empty-start SQLite databases and first-administrator Setup. |
| A coordinator selected students for normal registration | Replaced with authenticated, student-owned invoice and self-registration flows. |
| Payment rules were fixed around demonstration data | Made the 30%, 80%, 100%, late-fee, and course/progression rules configurable by an administrator. |
| Study modes did not control financial information | Added per-mode fees, maximum loads, payment accounts, and Evening timetable treatment. |
| Timetable checks focused on display | Added capacity, room, lecturer, availability, ordinary-student, and carry-course conflict evaluation. |
| Disabled accounts could retain an old session | Added active-account verification on every protected request. |

## Deliberate prototype boundaries

- The system does not connect to the CBU portal, a bank, or Zanaco Bill Muster.
- A recorded payment is administrator-confirmed prototype data; no payment is initiated or verified electronically.
- SQLite is used locally for demonstration, not as an institutional production database.
- The timetable engine is a transparent greedy heuristic, not a full mathematical optimization solver.
- The prototype has no public Student signup, password reset, email notification, audit trail, or multi-factor authentication.
- The delivered ZIP is intentionally empty of personal information and test databases.

## Recommended next improvements

1. Replace EnsureCreated with Entity Framework migrations, scheduled backups, and controlled institutional deployment.
2. Integrate approved institutional identity and payment services only after authorization, security review, and data-protection approval.
3. Add password reset, account recovery, email notifications, audit logs, and stronger administrative approval controls.
4. Add course editing, registration withdrawal, academic-history import, and reporting workflows after the core rules are formally validated.
5. Extend timetable optimization with constraints for larger cohorts, lecturer preferences, multiple meeting sessions, and measurable optimization quality.
6. Conduct usability tests with representative users and load tests using only approved anonymized data.
