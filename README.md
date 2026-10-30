# ClinicBookingSystem

PHCIS clinic booking: patients find clinics, view free time slots, book, cancel and reschedule, and receive a confirmation.

* Requirements: [docs/architecture/requirements.md](docs/architecture/requirements.md)
* Architecture, ERD and sequence diagram: [docs/architecture/architecture.md](docs/architecture/architecture.md)

## Stack

Blazor WASM + MudBlazor, ASP.NET Core minimal API, EF Core (SQL Server), martinothamar/Mediator (source generated),
FluentValidation, ASP.NET Identity + JWT, Serilog, xUnit v3 + Shouldly + NSubstitute, Playwright.

## Run

Prerequisites: .NET 10 SDK, SQL Server LocalDB.

```powershell
# one-time: JWT signing key for local development
dotnet user-secrets set "Jwt:Key" "<at least 32 random characters>" --project ClinicBookingSystem.Api

dotnet run --project ClinicBookingSystem.Api --urls http://localhost:5100          # migrates + seeds demo clinics/slots, Swagger at /swagger
dotnet run --project ClinicBookingSystem.BlazorWasm --urls http://localhost:5271   # UI
```

Seeded admin (development and Docker only): `admin@phcis.local` / `Admin#12345` (override with `Seed:AdminPassword`).
Admins and clinic staff sign in to the **Manage clinics** page (add clinics, open time slots); patients sign in to the booking pages.

## Test

```powershell
dotnet test --project ClinicBookingSystem.UnitTests

# Integration tests: real API + real SQL Server in a Testcontainers container (Docker must be running)
dotnet test --project ClinicBookingSystem.IntegrationTests
```

A fresh SQL Server container can take several minutes to start on slower machines. Set `TESTCONTAINERS_REUSE=true` to keep one container
between runs (each run still uses its own database); remove it with `docker rm -f` when you no longer need it.

```powershell
# E2E (headless Chromium, run serially): start the stack first, e.g. the Docker setup below, then
dotnet build ClinicBookingSystem.E2ETests
pwsh ClinicBookingSystem.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium   # once
dotnet test --project ClinicBookingSystem.E2ETests
```

## Docker

Runs SQL Server, the API and the UI together with no setup (UI http://localhost:5271, API + Swagger http://localhost:5100/swagger):

```powershell
docker compose -f deploy/docker/docker-compose.yml up --build
```

The API runs in the `Docker` environment and reads [appsettings.Docker.json](ClinicBookingSystem.Api/appsettings.Docker.json)
(connection string, JWT key, seeded admin, CORS). Those values are for local use only; production must supply its own through environment variables.
The `Docker` environment migrates the database and seeds demo clinics, slots and the admin account.

The first start of a fresh SQL Server volume can take several minutes (system database upgrades); the API waits for the SQL healthcheck, so be patient.
Stop with `docker compose -f deploy/docker/docker-compose.yml down` (add `-v` to also delete the database volume, e.g. after changing the SA password).

## Migrations

```powershell
dotnet ef migrations add <Name> --project ClinicBookingSystem.<Module>.Persistence --startup-project ClinicBookingSystem.Api --context <Module>DbContext --output-dir Migrations
```

The identity context uses `--project ClinicBookingSystem.Api --context AppIdentityDbContext --output-dir Identity/Migrations`.