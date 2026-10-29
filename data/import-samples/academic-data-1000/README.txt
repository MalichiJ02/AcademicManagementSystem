ACADEMIC MANAGEMENT CSV DATASET

Source: CBU Lusaka Campus 2026 Semester 2 DAY and EVENING timetable, normalized by the project's CbuTimetableCatalog.
Generated records: 1000 students, 12 programmes, 126 courses, 61 lecturers, 14 rooms, 126 course-lecturer assignments, 126 primary timetable entries, and 3030 student-course registrations.

IMPORT ORDER FOR A NEW/EMPTY DATABASE
1. 01_programmes.csv
2. 02_lecturers.csv
3. 03_rooms.csv
4. 04_courses.csv
5. 05_course_lecturer_assignments.csv
6. 06_students.csv

The current application has import pages for programmes, lecturers, rooms, courses, course-lecturer assignments, and students.
The registrations, timetable, and study modes files are reference datasets because the current application has no matching CSV import page.
If the supplied QA seed is already loaded, the academic setup tables already exist. Import only 06_students.csv to avoid duplicate-code errors.
The course CSV uses Full-time because the course import page requires an active study mode; the built-in seed catalogue leaves course study mode unrestricted.
All names and student email addresses are fictional test data.

PROGRAMME DISTRIBUTION
BCS: 84
BCSE: 84
BIS: 84
BIT: 84
BBA: 83
BAC: 83
BBF: 83
BPS: 83
BHRM: 83
BEC: 83
BIP: 83
LLB: 83
