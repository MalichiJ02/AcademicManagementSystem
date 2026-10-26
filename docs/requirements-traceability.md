# Requirements Traceability

This table links the agreed project requirements to the delivered prototype and its verification evidence.

| Requirement | Delivered implementation | Evidence |
|---|---|---|
| Separate Administrator and Student logins | ASP.NET Core Identity roles: Administrator, Student, and Coordinator; role-protected Razor Pages. | T02, T03, T04 |
| Empty initial database | Database initialization creates schema and roles only; no students, courses, payments, invoices, or timetable records are seeded. | T01 |
| Administrator manages core academic data | Administrator pages manage students/accounts, programmes, study modes, payment accounts, settings, courses, prerequisites, rooms, lecturers, and staff roles. | T05 |
| Student generates own invoice | Student/Invoices creates one current-period invoice from the linked programme and study mode. | T08 |
| Student sees finance history | Student portal shows current and previous invoices, balances, payment percentages, payment records, and configured payment account. | T08, T09 |
| Student self-registration | Student/Registration accepts only the signed-in student's selection; no coordinator registration path is used. | T03, T09, T10 |
| 30% registration gate | Configurable registration threshold defaults to 30%; access is checked before registration. | T09 |
| 80% exam-slip gate | Configurable threshold defaults to 80% and requires confirmed registration. | T11 |
| 100% results gate | Configurable threshold defaults to 100% and requires a published result. | T12, T13 |
| K600 late-registration fee | Academic settings include a configurable late-registration fee of ZMW 600; invoice generation applies it only when late registration is enabled. | Configuration review |
| Full-time, Part-time, Evening modes | Study modes store description, maximum course count, additional fee, and payment account; Part-time can be configured to one or two courses. | T05, T08 |
| Prerequisites and progression | Multiple prerequisite links, completed-course history, carry classification, four-course total, two-carry normal limit, and more-than-two failure restriction are enforced by RegistrationService. | T10 and rule review |
| Carry courses in suitable semester | Registration validates semester offering; current-period timetable setup only exposes active courses offered in that semester. | Rule review |
| Timetable conflict minimization | TimetableService validates rooms, lecturers, availability, capacity, ordinary student conflicts, and carry-course conflicts. The automatic allocator scores ordinary conflicts first, then carry conflicts. | T06, T14 |
| Student academic information | Student pages show registered courses, timetable, attendance, exam-slip status, and published results. | T10 to T14 |

## Requirement source

The table is based on the approved project specification: administrator-managed empty database, individual student accounts, student-generated invoices and self-registration, configurable payment thresholds, study modes, academic progression rules, and timetable optimization.
