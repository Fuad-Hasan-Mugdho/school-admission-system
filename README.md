# School Admission System

Online first-come-first-served admission for a private school: a parent applies for a seat in a
class/shift, the seat is held for a limited time, paying the fee confirms it, and an unpaid hold
releases the seat automatically.

| Layer | Stack |
|---|---|
| Backend | C# / .NET 8 minimal API, Entity Framework Core 8, PostgreSQL 16 |
| Frontend | React 18, TypeScript, Vite, React Router (no UI framework) |
| Tests | xUnit + WebApplicationFactory + Testcontainers (backend), Vitest + Testing Library (frontend) |
| Runtime | Docker Compose |

Repository layout:

```
backend/    .NET solution: src/SchoolAdmission.Api, tests/SchoolAdmission.Api.Tests, Dockerfile
frontend/   Vite app, Dockerfile (nginx), runtime config entrypoint
docker-compose.yml
ASSUMPTIONS.md   decisions where the brief was silent
AI_USAGE.md      how AI tooling was used
```

## Run with Docker (the only supported way for evaluation)

Prerequisite: Docker with the Compose plugin. Nothing else.

```bash
docker compose up --build
```

| What | URL |
|---|---|
| **Frontend (open this)** | http://localhost:3000 |
| Backend API | http://localhost:8080 |
| Swagger UI | http://localhost:8080/swagger |
| Health | http://localhost:8080/health |
| PostgreSQL | localhost:5432 (`admission` / `admission`, db `admission`) |

The first build takes a few minutes (NuGet + npm restore). Afterwards:

- `docker compose down` stops everything; data stays in the `postgres-data` volume, so
  `docker compose up` brings back every class and application.
- `docker compose down -v` wipes the database too.

### Configuration

Set these in the shell (or a `.env` file next to `docker-compose.yml`) before `docker compose up`:

| Variable | Default | Meaning |
|---|---|---|
| `HOLD_DURATION_SECONDS` | `600` | How long an application holds a seat before it expires |
| `HOLD_SWEEP_INTERVAL_SECONDS` | `1` | How often expired holds are persisted as `EXPIRED` |
| `API_BASE_URL` | `http://localhost:8080` | Backend URL the **browser** should call (injected into the frontend at container start) |

Example: a 30-second hold window for demos:

```bash
HOLD_DURATION_SECONDS=30 docker compose up --build
```

The backend container itself reads `DATABASE_URL` (a `postgres://user:pass@host:5432/db` URL or an
Npgsql key=value string; `ConnectionStrings__Default` also works), `HOLD_DURATION_SECONDS`,
`HOLD_SWEEP_INTERVAL_SECONDS` and `ASPNETCORE_URLS`.

### Bonus: two backend processes on one database

```bash
docker compose --profile multi up --build
```

starts a second, identical backend on http://localhost:8081 sharing the same PostgreSQL. Applications
sent to both ports still never exceed `totalSeats` (why: see ASSUMPTIONS.md, "Concurrency guarantee").

## Using the app

1. Open http://localhost:3000.
2. **নতুন Class (admin)** → create e.g. `Class 6 - Morning Shift` with 120 seats. Every shift is its
   own class.
3. Type the student's 17-digit birth registration number in the box at the top. It is remembered in
   the browser.
4. **Class তালিকা** → counts refresh every 3 s → **আবেদন করুন**.
5. The application page shows the status, a live countdown, **ভর্তি ফি দিন** (confirms admission,
   no real payment) and **বাতিল**. When the countdown hits zero the page shows `EXPIRED` and the pay
   button is disabled, without reloading.
6. **আমার আবেদন** lists every application of that student with its status, also after a page refresh
   or a new browser session.

## API

Base URL `http://localhost:8080`, JSON everywhere. Student identity is the `X-Student-Id` header
(17-digit birth registration number).

| # | Method & path | Purpose | Success | Errors |
|---|---|---|---|---|
| 1 | `POST /classes` | create class `{ "name", "totalSeats" }` | `201 { id, name, totalSeats }` | `400` |
| 2 | `GET /classes` | all classes with seat accounting | `200 [ { id, name, totalSeats, heldSeats, confirmedSeats, availableSeats } ]` | |
| 3 | `GET /classes/{classId}` | one class | `200 { … same fields … }` | `404` |
| 4 | `POST /classes/{classId}/holds` | apply (hold a seat) — header required | `201 { holdId, classId, studentId, status: "HELD", expiresAt }` | `400`, `404`, `409 SEAT_UNAVAILABLE`, `409 ALREADY_APPLIED` |
| 5 | `GET /holds/{holdId}` | current state | `200 { holdId, classId, status, expiresAt }` | `404` |
| 6 | `POST /holds/{holdId}/confirm` | pay fee — header required | `200 { holdId, status: "CONFIRMED" }` (idempotent) | `400`, `403`, `404`, `409 HOLD_EXPIRED / HOLD_CANCELLED` |
| 7 | `DELETE /holds/{holdId}` | cancel — header required | `204` | `400`, `403`, `404`, `409 HOLD_CONFIRMED` |
| 8 | `GET /me/holds` | this student's applications — header required | `200 [ { holdId, classId, className, status, expiresAt } ]` | `400` |

`status` is one of `HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`. Responses contain a few extra
fields beyond the contract (`serverTime`, `className`, `studentId`, `createdAt`); nothing from the
contract is renamed or omitted.

Every error has the same JSON shape:

```json
{ "error": "SEAT_UNAVAILABLE", "message": "No seats are available in this class right now." }
```

Quick smoke test from a shell:

```bash
curl -s -X POST localhost:8080/classes -H 'Content-Type: application/json' -d '{"name":"Class 6 - Morning Shift","totalSeats":2}'
```

```bash
curl -s -X POST localhost:8080/classes/<classId>/holds -H 'X-Student-Id: 20152691234567890'
```

## Running the tests

### Backend (xUnit, 26 tests, real PostgreSQL)

The suite boots the real API in-process against a real PostgreSQL. It includes:

- `ConcurrencyTests.ManyParallelHolds_NeverOversellSeats` — 120 simultaneous applications for 7 seats
- `ConcurrencyTests.TwoBackendInstances_SharingOneDatabase_NeverOversell` — two API hosts, one DB
- `ConcurrencyTests.MixedHoldCancelConfirmStorm_KeepsInvariant`, `SameStudent_ParallelHolds_OnlyOneSucceeds`
- expiry (short `HOLD_DURATION_SECONDS`), idempotent confirm, owner checks, validation, contract shapes

**With Docker only (recommended):**

```bash
docker compose --profile test run --build --rm backend-tests
```

**With the .NET 8 SDK installed** (a throw-away PostgreSQL container is started automatically through
Testcontainers, so Docker must be running):

```bash
cd backend && dotnet test
```

To point the tests at an existing database instead, set `TEST_DATABASE_URL` (Npgsql format), e.g.
`Host=localhost;Port=5432;Database=admission_test;Username=test;Password=test`.

### Frontend (Vitest + Testing Library, 22 tests)

```bash
cd frontend && npm ci && npm test
```

Covers: seat table rendering, apply flow and `X-Student-Id` header, 409 messaging, disabled apply
without a valid id, countdown reaching zero → `EXPIRED` + disabled pay button without reload, confirm,
two-step cancel, foreign-student guard, server-unreachable messaging, error-message mapping, form
validation.

## Local development without Docker (optional)

```bash
docker compose up postgres
```

```bash
cd backend/src/SchoolAdmission.Api && DATABASE_URL=postgres://admission:admission@localhost:5432/admission dotnet run
```

```bash
cd frontend && npm ci && npm run dev
```

The Vite dev server runs on http://localhost:3000 and calls `VITE_API_BASE_URL`
(default `http://localhost:8080`; copy `frontend/.env.example` to `frontend/.env` to change it).

## Design notes (short)

- **Oversell protection** is a per-class `SELECT … FOR UPDATE` inside the hold transaction; a partial
  unique index enforces one active application per student per class. Details in ASSUMPTIONS.md.
- **Expiry** is evaluated on read (`HELD` + past `expiresAt` ⇒ `EXPIRED`) and persisted by a 1-second
  background sweeper, so seats return on time without any client call.
- **Durability**: PostgreSQL volume + EF Core migrations applied at startup (under an advisory lock, with
  retry while the database boots).
