# Architecture

## Style: modular monolith

One deployable API hosts four business modules. Each module has its own Domain, Application and Persistence
project and its own `DbContext`/database schema. Modules never reference each other's Domain or Persistence;
they talk through the other module's **Application layer** by sending Mediator commands/queries
(e.g. Appointments sends `ReserveTimeSlotCommand` to TimeSlots). Any module can later be extracted into a service
by replacing those messages with HTTP/queue calls.

```mermaid
flowchart LR
    Blazor["Blazor WASM (MudBlazor)"] -->|HTTPS + JWT| Api["ASP.NET Core Minimal API"]
    Api --> Mediator
    subgraph Modules
        direction TB
        Patients["Patients"]
        Clinics["Clinics"]
        TimeSlots["TimeSlots"]
        Appointments["Appointments"]
    end
    Mediator --> Appointments
    Mediator --> Patients
    Mediator --> Clinics
    Mediator --> TimeSlots
    Appointments -. "Mediator messages" .-> TimeSlots
    Appointments -. "Mediator messages" .-> Clinics
    Modules --> SQL[("SQL Server: schemas patients, clinics, timeslots, appointments, identity")]
```

## Dependencies inside a module

`Domain  <-  Application  <-  Persistence`, composed by the API host. Shared building blocks:

* `ClinicBookingSystem.SharedKernel` – entity base type, `DomainException`, `NotFoundException`, `ConflictException`
* `ClinicBookingSystem.Contracts` – DTOs, request models and FluentValidation validators shared by API and Blazor

NuGet versions are centralised in `Directory.Packages.props`; layer-wide package references
(Mediator, FluentValidation, EF Core, test stack) and project references between layers live in `Directory.Build.props`.

## Data model

```mermaid
erDiagram
    IDENTITY_USER ||--o| PATIENT : "IdentityUserId"
    CLINIC ||--o{ TIME_SLOT : offers
    PATIENT ||--o{ APPOINTMENT : books
    TIME_SLOT ||--o| APPOINTMENT : "reserved by (one confirmed)"
    CLINIC ||--o{ APPOINTMENT : hosts

    PATIENT {
        guid Id PK
        guid IdentityUserId UK
        string FirstName
        string LastName
        string Email
        string PhoneNumber
        date DateOfBirth
    }
    CLINIC {
        guid Id PK
        string Name
        string Address
        string PhoneNumber
        bool IsActive
    }
    TIME_SLOT {
        guid Id PK
        guid ClinicId "UK with StartsAt"
        datetimeoffset StartsAt
        datetimeoffset EndsAt
        bool IsReserved
        rowversion RowVersion
    }
    APPOINTMENT {
        guid Id PK
        guid PatientId
        guid ClinicId
        guid TimeSlotId "unique where Status = Confirmed"
        datetimeoffset StartsAt
        datetimeoffset EndsAt
        string Status "Confirmed | Cancelled"
        string ConfirmationNumber UK
        datetimeoffset BookedAt
        datetimeoffset CancelledAt
    }
```

Cross-module references are plain Guids (no foreign keys across schemas) to keep modules independently deployable.

## Booking sequence

```mermaid
sequenceDiagram
    actor P as Patient
    participant UI as Blazor UI
    participant API as Minimal API
    participant A as Appointments
    participant T as TimeSlots
    participant C as Clinics
    P->>UI: choose clinic, date, slot, Confirm
    UI->>API: POST /api/appointments (JWT)
    API->>A: BookAppointmentCommand(patient, slot)
    A->>T: GetTimeSlotQuery
    A->>A: patient already booked at that time?
    A->>T: ReserveTimeSlotCommand (row-version check)
    alt slot already taken
        T-->>A: ConflictException
        A-->>API: 409 Conflict
        API-->>UI: "This time slot has already been booked."
    else reserved
        A->>A: save Appointment (unique index on slot)
        A->>C: GetClinicQuery
        A-->>API: confirmation DTO + message
        API-->>UI: 201 Created
        UI-->>P: "Your appointment at ... is confirmed. Confirmation number APT-XXXXXXXX"
    end
```

If saving the appointment fails after the slot was reserved, the handler releases the slot (compensation), because
the two modules use separate `DbContext`s and therefore separate transactions.

## Double-booking defence (three layers)

1. Domain: `TimeSlot.Reserve` throws when the slot is already reserved.
2. Concurrency: `TimeSlot.RowVersion` makes simultaneous reservations fail with a concurrency conflict (HTTP 409).
3. Database: unique filtered index on `Appointments.TimeSlotId` where `Status = 'Confirmed'`.

## Error handling

`ApiExceptionHandler` maps `ValidationException` -> 400, `NotFoundException` -> 404, `ConflictException` -> 409,
`DomainException` -> 422 as RFC 7807 problem details. A Mediator `ValidationBehavior` runs FluentValidation validators
before every handler.

## Testing strategy

| Level | Project | Tools |
|---|---|---|
| Unit (domain + handlers) | `ClinicBookingSystem.UnitTests` | xUnit v3, Shouldly, NSubstitute |
| End-to-end (browser) | `ClinicBookingSystem.E2ETests` | Playwright, Shouldly |
