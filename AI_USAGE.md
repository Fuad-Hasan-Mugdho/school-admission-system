# AI_USAGE

**Tool:** Claude (Anthropic, Claude Code in the desktop app). Used as a pair programmer for the whole
assignment: reading the brief, proposing the design, writing backend/frontend/tests/Docker files, and
running builds and tests in Docker. All design decisions in ASSUMPTIONS.md were reviewed and are mine.

## Where it was used

| Area | How |
|---|---|
| Brief | Extracted the PDF text (it was garbled by overlapping text layers, so pages were rendered to images and read visually) and turned it into a requirement checklist. |
| Backend | Domain model, EF Core mapping + migration, hold service with the `FOR UPDATE` strategy, endpoints, error envelope, startup migration with advisory lock, expiry sweeper. |
| Tests | xUnit suite incl. the concurrency and two-instance tests; Vitest suite for the UI. |
| Frontend | Pages, polling hook, countdown hook, Bangla error mapping, runtime config via `config.js`. |
| Ops | Dockerfiles, nginx config, docker-compose with `multi`/`test` profiles, README. |

## Where the AI was wrong and how it was caught

Every one of these was caught by actually building and running the code, not by reading it.

1. **Testcontainers API.** It wrote `new PostgreSqlBuilder("postgres:16-alpine")`; that constructor does
   not exist in Testcontainers 4.x (`new PostgreSqlBuilder().WithImage(...)`). Caught by the first
   `dotnet build`.
2. **Test configuration never reached `Program.cs`.** The test factory used
   `ConfigureAppConfiguration` to inject `DATABASE_URL`, but with minimal hosting those callbacks run at
   `builder.Build()`, *after* `Program.cs` had already read the connection string, so every test failed
   with "No database connection configured". Fixed by passing values with `UseSetting`, which
   `WebApplicationFactory` forwards as command-line arguments and are therefore visible immediately.
3. **Malformed JSON produced an empty 400.** The AI assumed the global exception handler would turn a bad
   request body into the JSON error envelope. ASP.NET Core minimal APIs swallow body-binding failures
   and return a bare 400 unless `RouteHandlerOptions.ThrowOnBadRequest` is enabled. The
   `CreateClass_InvalidInput_Returns400_WithJsonError` test (cases `not json at all` and
   `"totalSeats":"lots"`) exposed it.
4. **TypeScript: `unknown && <JSX/>`.** Rendering `{state.error && <Alert/>}` where `error` is `unknown`
   does not type-check (`unknown` is not a `ReactNode`). `tsc` caught it; changed to a ternary.
5. **Countdown rendered one tick late.** The first `useCountdown` stored the remaining time in state set
   from an effect, so the first render after data arrived showed "—" and a test asserting synchronously
   after `waitFor` was flaky. The hook was rewritten to derive the value during render and only use a
   timer to force re-renders. A dedicated hook test was added.

6. **The concurrency test deadlocked itself.** To fire all requests "at the same moment" the AI used a
   `ManualResetEventSlim` and called `gate.Wait()` inside each `async` lambda. The lambdas run
   synchronously up to their first `await`, so the very first one blocked the thread that was still
   creating the others and `gate.Set()` was never reached: the test run hung silently after the first
   test class. Found because the test process never finished; fixed with an awaitable
   `TaskCompletionSource` gate (`await gate.Task`).

7. **The test suite exhausted PostgreSQL's connections.** Every `WebApplicationFactory` gets its own
   Npgsql pool, idle connections linger after the host is disposed, and the concurrency tests open
   many connections at once; after a dozen factories PostgreSQL answered "too many clients already"
   and later test hosts could not even run their migrations. Seen in the test log as endless
   "Database not ready" retries. Fixed by capping the pool per test host (16) and clearing pools on
   dispose, and by giving the API itself a 40-connection default pool so two backends sharing one
   database stay under `max_connections = 100`.

## What I checked myself

- Ran the full backend suite against a real PostgreSQL (SDK container) and the frontend suite with
  Vitest until both were green.
- `docker compose up --build`, then exercised every endpoint with `curl`: contract shapes, every error
  code, idempotent confirm, cancel rules, and a 60-request burst against 2 free seats (2×201, 58×409).
- `docker compose down` → `HOLD_DURATION_SECONDS=20 docker compose --profile multi up`: all classes and
  the confirmed admission were still there; a 40-request burst split across ports 8080/8081 for 4 seats
  gave exactly 4×201 / 36×409.
- Walked through the UI in a browser: enter student id → apply → countdown → pay → confirmed; admin
  page creates a class; with the 20-second window the application page flipped to `EXPIRED` and
  disabled both buttons without a reload; "আমার আবেদন" survived the backend restart.
