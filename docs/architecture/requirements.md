# Requirements Analysis

## Stakeholders

| Stakeholder | Interest |
|---|---|
| Patient | Find a clinic, see free slots, book / cancel / reschedule, receive confirmation |
| Clinic staff | Open and maintain time slots for their clinic, see bookings |
| System admin | Manage clinics and users, monitor the system |
| PHCIS / Department of Health | Reliable, secure handling of patient data |

## Functional requirements

| ID | Requirement | Status |
|---|---|---|
| FR1 | A patient can register an account and sign in | Implemented |
| FR2 | A patient can list clinics | Implemented |
| FR3 | A patient can view available time slots for a clinic and date | Implemented |
| FR4 | A patient can book an available slot and receives a confirmation message and number | Implemented |
| FR5 | A slot can never be booked twice (no double booking) | Implemented (domain rule, optimistic concurrency token, unique filtered DB index) |
| FR6 | A patient cannot hold two overlapping appointments | Implemented |
| FR7 | A patient can cancel a future appointment; the slot becomes available again | Implemented |
| FR8 | A patient can reschedule to another free slot at the same clinic | Implemented |
| FR9 | Admin can create clinics; admin/clinic staff can open a day of slots | Implemented (API) |
| FR10 | Reminders (SMS/email), calendar sync, real-time updates | Out of scope (optional enhancements) |

## Non-functional requirements

| Area | Requirement | How it is addressed |
|---|---|---|
| Security | Authenticated access, role-based authorisation, hashed passwords, lockout | ASP.NET Core Identity + JWT bearer; roles `Patient`, `ClinicStaff`, `Admin`; patients can only touch their own appointments |
| Security | Secrets are not in source control | `Jwt:Key` comes from user-secrets / environment |
| Privacy | Patient data is separated from login data | Separate `patients` and `identity` schemas |
| Performance | Slot queries are index-backed | Index on `(ClinicId, StartsAt)` |
| Scalability | Modules can later be extracted into services | Modular monolith, one schema and one public contract per module |
| Reliability | Concurrent bookings are safe | Row-version concurrency token plus compensation if the appointment save fails |
| Maintainability | SOLID, testable | Domain/Application/Persistence layering, handlers depend on abstractions |
| Observability | Structured logging | Serilog (console + rolling file), request logging |
| Usability | Immediate, clear feedback | MudBlazor UI, FluentValidation messages, problem-detail errors shown to the user |
