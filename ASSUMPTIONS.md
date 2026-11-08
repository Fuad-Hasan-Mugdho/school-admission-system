# ASSUMPTIONS

Things the brief leaves open, what I chose, and why.

## Identity & validation

- **`X-Student-Id` must be exactly 17 digits.** The brief says the value *is* the 17-digit birth
  registration number, so the API rejects anything else with `400 STUDENT_ID_REQUIRED`. The UI only
  lets you type digits and enables "আবেদন করুন" once 17 are present.
- **No login, so anyone who knows a number can act on that student's applications.** That is what the
  brief asks for; the API still enforces that confirm/cancel are performed *with* the owning student's
  id (`403 NOT_OWNER` otherwise).
- **Class names are not unique.** Two admins might legitimately create "Class 6 - Morning Shift" twice
  by accident, but automated tests (ours and the reviewers') also create classes repeatedly with the
  same name; a uniqueness rule would make the API brittle for no business gain.
- **`totalSeats` must be a positive integer ≤ 100 000.** Zero/negative seats make no sense; the upper
  bound only guards against typos.

## Hold lifecycle

- **Status is derived, not just stored.** A `HELD` row whose `expiresAt` has passed is reported as
  `EXPIRED` by every read and is ignored by every seat count, even if nothing has written to the row
  yet. A background sweeper additionally persists `EXPIRED` once per second (configurable with
  `HOLD_SWEEP_INTERVAL_SECONDS`) so the stored data stays truthful. Correctness therefore never depends
  on a timer firing, and no client ever has to call an "expire" endpoint.
- **Confirming an already-confirmed hold returns `200 CONFIRMED` again** (and increments nothing).
  Double clicks and retried requests must be harmless.
- **Confirm is rejected with `409`** when the hold is `EXPIRED` (`HOLD_EXPIRED`) or `CANCELLED`
  (`HOLD_CANCELLED`); 409 because the request is well-formed but conflicts with the resource's state.
- **`DELETE /holds/{id}` cancels only applications, not admissions.** `HELD → CANCELLED` frees the
  seat and returns `204`. Cancelling something that is already `EXPIRED` or `CANCELLED` is an idempotent
  `204`. A `CONFIRMED` admission returns `409 HOLD_CONFIRMED`: the fee has been "paid", so undoing it is a
  school-office decision (refund etc.), not a button for the parent.
- **One active application per student per class** means one `HELD` *or* `CONFIRMED` row. After a hold
  expires or is cancelled the same student may apply again. Different classes (including the other
  shift of the same grade) are independent.
- **`404` vs `403`.** Unknown ids are `404`; a real hold that belongs to someone else is `403`.
  Hiding existence behind a `404` would make the UI's "this belongs to another student" hint impossible.

## Concurrency guarantee (and the bonus: two backend processes)

- The hold-creation path runs in a database transaction that first executes
  `SELECT ... FROM classes WHERE id = @id FOR UPDATE`. Every competing request for the same class
  queues on that PostgreSQL row lock, so "expire stale rows → check duplicate → count active → insert"
  is strictly serialised **per class** while different classes proceed in parallel.
- Because the lock lives in PostgreSQL rather than in process memory, it works identically for one
  process, two processes on different ports (`docker compose --profile multi up`) or N replicas.
  `ConcurrencyTests.TwoBackendInstances_SharingOneDatabase_NeverOversell` boots two independent API
  hosts against one database and fires 100 simultaneous applications at 6 seats.
- A partial unique index `(class_id, student_id) WHERE status IN ('HELD','CONFIRMED')` backs the
  per-student rule at the storage level as defence in depth.
- Confirm and cancel are single conditional `UPDATE ... WHERE status = 'HELD' AND expires_at > now`
  statements, so a confirm racing the expiry sweeper (or a second confirm) cannot both win.
- Schema migrations run at startup under a `pg_advisory_lock`, so two backends starting at the same
  moment do not race each other on `CREATE TABLE`.
- Migrations are also retried with back-off because PostgreSQL can accept TCP connections a moment
  before it is ready to serve queries.
- Unless the connection string says otherwise, each backend process caps its connection pool at 40
  (Npgsql's default is 100, equal to PostgreSQL's default `max_connections`), so two backend processes
  plus the sweeper and migrations fit comfortably in a stock PostgreSQL. Requests beyond the cap wait
  briefly for a pooled connection; they are not rejected.

## Time

- All timestamps are UTC ISO-8601 (`2026-11-01T04:10:00Z`-style) as in the brief's examples.
- Responses carry an extra `serverTime` field (additions are allowed by the contract). The UI measures
  the countdown against the server's clock, so a wrong PC clock cannot make a dead hold look alive.
- Expiry is checked with the API host's clock. In Docker all containers share the host kernel clock;
  with several backends on different machines they should be NTP-synchronised, as any cluster is.

## UI

- Bangla is the primary language because the required button labels in the brief are Bangla; ids and
  technical nouns stay in English.
- The "class list" and "my applications" pages poll every 3 s, the application page every 2 s, and the
  countdown ticks locally twice per second. When the countdown reaches zero the page shows `EXPIRED` and
  disables "ভর্তি ফি দিন" immediately, then re-fetches to let the server confirm.
- The student id is kept in `localStorage`, so closing the browser and coming back still shows the same
  student's applications.
- An "admin" page is just another route (`/admin/classes/new`); the brief asks for no login anywhere.

## Configuration

| Variable | Where | Default | Purpose |
|---|---|---|---|
| `DATABASE_URL` or `ConnectionStrings__Default` | backend | – (required) | PostgreSQL connection (`postgres://…` URL or Npgsql key=value) |
| `HOLD_DURATION_SECONDS` | backend | `600` | How long a hold reserves a seat |
| `HOLD_SWEEP_INTERVAL_SECONDS` | backend | `1` | How often `EXPIRED` is persisted |
| `ASPNETCORE_URLS` | backend | `http://+:8080` | Listen address |
| `API_BASE_URL` | frontend container | `http://localhost:8080` | Backend URL injected at container start |
| `VITE_API_BASE_URL` | frontend dev/build | `http://localhost:8080` | Backend URL for `npm run dev` |

## Identifiers

- Ids are opaque strings with a type prefix: classes `c_xxxxxxxxxxxx`, holds `h_xxxxxxxxxxxx`
  (12 random base-36 characters, ~62 bits). They are not guessable and reveal nothing about volume.
