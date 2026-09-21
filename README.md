# Library API

A small library system that answers four analytical questions about lending, split across an HTTP API
and a service layer that talk to each other over gRPC.

## Quick start

**.NET 10 SDK** is the only prerequisite — no Docker, no database to install, no certificates to trust.
Everything restores on its own, so a fresh clone needs no step before these.

```bash
dotnet test                                # 272 tests, ~20s warm and under a minute from cold
```

Two hosts, one terminal each, started in either order:

```bash
dotnet run --project src/Library.Service   # terminal 1 — gRPC on http://localhost:5210
dotnet run --project src/Library.Api       # terminal 2 — HTTP on http://localhost:5100
```

Then, from a third, the four questions and the warm-ups:

```bash
curl "http://localhost:5100/api/books/most-borrowed?limit=5"                  # Q1
curl "http://localhost:5100/api/borrowers/top?from=2026-01-01&to=2026-04-01"  # Q2
curl "http://localhost:5100/api/borrowers/3/reading-pace"                     # Q3
curl "http://localhost:5100/api/books/2/also-borrowed?limit=3"                # Q4
dotnet run --project src/Library.Warmups                                      # warm-ups 1-4
```

SQLite is created and seeded on first run. API reference at <http://localhost:5100/docs>; the same
calls worked through in [`requests.http`](requests.http), and again with assertions on the expected
answers in the Bruno collection under [`bruno/`](bruno) — open that folder in Bruno, with no
environment to select. Start only the API and every `/api` call answers 503 with a problem document
naming the endpoint it tried and the command that starts the other host. The writes among those calls
mutate the seeded data: to get back to a clean slate, stop the service, delete
`src/Library.Service/library.db*`, and start it again.

## Architecture

```
client ──HTTP/JSON──► Library.Api :5100 ──gRPC over h2c──► Library.Service :5210 ──► SQLite library.db
```

`Library.Api` owns HTTP only: routing, binding, JSON shapes, OpenAPI, and translating a gRPC status into
an RFC 9457 problem document. Its single project reference, to `Library.Contracts`, means it cannot reach
the domain or the database — `DependencyRuleTests` reads the compiled manifests and fails if that changes.
Two processes, because gRPC only means something across a process boundary. Deployment is two containers
with the service unexposed, migrations as an `ef migrations bundle` job, config by environment variable.

## The four questions

Windows filter on the borrow date and are half-open `[from, to)`; dates are ISO `yyyy-MM-dd` read as UTC
midnight; `limit` defaults to 10 and is rejected above 100, not clamped; every ranking ends in `id
ascending`, so the top-N is stable. Seed data is hand-designed so every answer can be checked on paper.

| Q | Endpoint | Ranked by |
| --- | --- | --- |
| 1 | `GET /api/books/most-borrowed?from&to&limit` | loans, distinct borrowers, id |
| 2 | `GET /api/borrowers/top?from&to&limit` — window **required**, else 400 | loans, distinct titles, id |
| 3 | `GET /api/borrowers/{id}/reading-pace?from&to` | Σ pages ÷ Σ days |
| 4 | `GET /api/books/{id}/also-borrowed?from&to&limit` | shared borrowers, cohort loans, id |

Q3 counts completed loans only, in whole days rounded up with a minimum of one, and returns `null` rather
than `0.0` when there is nothing to estimate from; overlapping loans are not netted out, so Ava's January
is 82.0 pages/day where merged intervals would give 102.5. Q4 windows the ranked list but not the cohort,
and returns `cohortSize` — "3 people also borrowed this" means one thing out of 5 and another out of 500.

## API reference

| Method | Route | Success | Route-specific failures |
| --- | --- | --- | --- |
| `GET` | `/api/books` | 200 | — |
| `POST` | `/api/books` | 201 + `Location` | — |
| `GET` | `/api/books/{id}` | 200 | 404 |
| `POST` | `/api/borrowers` | 201 + `Location` | 409 |
| `GET` | `/api/borrowers/{id}` | 200 | 404 |
| `POST` | `/api/loans` | 201 + `Location` | 404, 409 |
| `GET` | `/api/loans/{id}` | 200 | 404 |
| `POST` | `/api/loans/{id}/return` | 200 | 404, 409 |
| `GET` | `/health` | 200 | — |

The four report routes answer 200, and `reading-pace` and `also-borrowed` also 404. Every failure is an
RFC 9457 problem document. 400 `InvalidArgument`, 503 `Unavailable` and 504 `DeadlineExceeded` (the
10-second deadline) apply to every route; 404 is `NotFound`; 499 a client that went away; 500 a defect;
409 `FailedPrecondition` — every copy out, at the loan limit, already holds the title, already returned,
duplicate email, lost a race. **400 means fix the request, 409 means it may succeed later.**

## Data model

`Book 1─* BookCopy 1─* Loan *─1 Borrower`; `Loan.BookId` is denormalised and immutable so each report
stays one `GROUP BY`, and `Loan.ReturnedAt IS NULL` **is** the state machine — no status enum can drift
out of step with it. Two rules live in the schema, because an `if` in C# cannot close a check-then-act
race: filtered unique indexes on `(BookCopyId)` and `(BorrowerId, BookId)` where `ReturnedAt IS NULL`, so
the loser of a race on the last copy gets a 409, and `ReturnedAt` doubles as a concurrency token against
a double return. Both port unchanged to SQL Server and MongoDB. The concurrent-loan limit is the one rule
with no database backstop — a burst of *n* borrows can overshoot it — accepted rather than paid for with
a serialisable transaction per borrow, and pinned by a characterisation test.

## Tests

`dotnet test` runs 272 tests with no setup: unit (93), integration on real SQLite built by the migration
(49), functional over the gRPC and HTTP contracts (87), system over both hosts on real sockets (16), and
warm-ups (27). No fixed ports and no host to start first — every tier brings up what it needs, and CI
runs the same command in Release.

```bash
dotnet test tests/Library.UnitTests                         # one tier
dotnet test --filter "FullyQualifiedName~LastCopyRace"      # one test
dotnet test --collect:"XPlat Code Coverage" --settings coverage.runsettings
```

Line coverage of `src/` is **97.7%**, branch 94.8%, excluding migrations and generated code. The 31
uncovered lines are all composition or design-time: the warm-ups' console `Main`, two private
constructors, `DesignTimeDbContextFactory` (only `dotnet ef` calls it), an unused test hook in
`ApiHost`, and the defensive fall-throughs in `DomainExceptionInterceptor` and `DatabaseErrors`.
[`docs/TESTING.md`](docs/TESTING.md) covers how each tier is wired and which tests to open first.

## Decisions and scope

[`docs/DECISIONS.md`](docs/DECISIONS.md) is one entry per decision with the alternative and the cost: SQL
over Mongo, SQLite over Docker, exceptions over `Result<T>`, no retries, where DDD stops. Deliberately not
built: auth and TLS (out of scope per the brief), Docker, paging and search, MediatR/CQRS/repositories,
retries, OpenTelemetry. Next would be lift instead of raw co-occurrence in Q4, overdue analytics off the
modelled-but-unread `DueAt`, and utilisation and dead stock. About 8 hours, including this README.
