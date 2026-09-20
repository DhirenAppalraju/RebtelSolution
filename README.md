# Library API

A small library system that answers four analytical questions about lending, split across an HTTP API
and a service layer that talk to each other over gRPC.

## Prerequisites

**.NET 10 SDK.** Nothing else — no Docker, no database to install, no certificates to trust.

| OS | Install |
| --- | --- |
| Windows | `winget install Microsoft.DotNet.SDK.10` |
| macOS | `brew install --cask dotnet-sdk` |
| Linux | `curl -sSL https://dot.net/v1/dotnet-install.sh \| bash -s -- --channel 10.0` |

## Quick start

```bash
dotnet test                                  # 252 tests, under a minute from cold, no setup

# terminal 1
dotnet run --project src/Library.Service     # gRPC on http://localhost:5210

# terminal 2
dotnet run --project src/Library.Api         # HTTP on http://localhost:5100
```

The database is a SQLite file created and seeded on first run. Open <http://localhost:5100/docs> for
the API reference, or run the calls in [`requests.http`](requests.http).

```bash
curl "http://localhost:5100/api/books"                                             # catalogue + availability
curl "http://localhost:5100/api/books/most-borrowed?limit=5"                       # Q1
curl "http://localhost:5100/api/borrowers/top?from=2026-01-01&to=2026-04-01"       # Q2
curl "http://localhost:5100/api/borrowers/3/reading-pace"                          # Q3
curl "http://localhost:5100/api/books/2/also-borrowed?limit=3"                     # Q4

dotnet run --project src/Library.Warmups                                           # the four warm-ups
```

Both hosts speak plain HTTP, so `dotnet run` never asks you to trust a development certificate:
security is out of scope by the brief. **If you start only the API**, every `/api` call answers `503`
with a problem document that names the endpoint it tried and the command that starts the other host.

### Where the brief is answered

| Requirement | Where |
| --- | --- |
| Four warm-up tasks | [`src/Library.Warmups`](src/Library.Warmups), tests in [`tests/Library.Warmups.Tests`](tests/Library.Warmups.Tests) |
| Q1 most borrowed books | `GET /api/books/most-borrowed` |
| Q2 top borrowers in a time frame | `GET /api/borrowers/top?from&to` |
| Q3 reading pace, pages per day | `GET /api/borrowers/{id}/reading-pace` |
| Q4 other books borrowed by this book's borrowers | `GET /api/books/{id}/also-borrowed` |
| Manage books, borrowers and lending | add book, add borrower, borrow, return, get by id, catalogue |
| Two layers, API and service | `Library.Api` and `Library.Service`, two processes |
| gRPC between them | [`src/Library.Contracts/Protos`](src/Library.Contracts/Protos), real HTTP/2 |
| SQL database | SQLite through EF Core, migrated and seeded on start-up |
| Four levels of automated tests | five test projects, `dotnet test`, no setup |

## Tests

```bash
dotnet test                                      # everything
dotnet test tests/Library.UnitTests              # one tier
dotnet test --collect:"XPlat Code Coverage" --settings coverage.runsettings
```

| Tier | The brief's words | What runs | The bug only this tier catches |
| --- | --- | --- | --- |
| Unit (91) | "individual methods/classes" | Pure code, no I/O | Wrong arithmetic, wrong rule, wrong mapping |
| Integration (42) | "database interactions" | Application services on real SQLite built by the migration | SQL that will not translate, work done in memory, constraints and races |
| Functional (75) | "key features" | Every feature through the service's gRPC contract, plus the HTTP contract with the service stubbed | Proto mapping, status codes, JSON shape, binding and validation |
| System (17) | "complete user flows" | Both hosts on real sockets, driven over HTTP | h2c negotiation, configuration, the whole path |
| Warm-ups (27) | page 1 of the brief | Four pure functions | Edge cases: `int.MinValue`, graphemes, overflow |

**Line coverage of `src/`: 96.8%** (1338 of 1381 lines), branch 92.2%, method 97.7% — measured with
coverlet across all five projects. Migrations, generated protobuf and generated logging code are
excluded, so the figure describes code that was written rather than code that was generated.

What the remaining 43 lines are, rather than leaving you to find out: `Library.Warmups/Program.cs`
and `DesignTimeDbContextFactory` are never executed by a test at all — one is a demo entry point
whose logic lives in the two classes it calls, the other exists only for `dotnet ef`. The rest is
the `MigrateOnStartup: false` branch, the generic fallback in `DatabaseErrors`, and a handful of
defensive arms that the three sealed `DomainException` subclasses make unreachable.

Three tests are worth opening on purpose:

- `ReportSqlTests` counts database round trips per report and asserts each report is a single
  `GROUP BY`. A report that quietly pulls rows into memory still returns the right answer on 28 loans,
  and fails here.
- `LendingServiceTests.LastCopyRace_ThroughTheService_BecomesAConflict` stages the check-then-act
  race two borrowers cause on the last copy, and drives it through `BorrowBookAsync` so the 409 the
  caller sees is the thing under test. The competing write is injected by a `SaveChanges`
  interceptor, so it lands between the read and the write every time: a race tested by timing is a
  test that passes or fails on timing.
- `DomainExceptionInterceptorTests.UnaryServerHandler_ClientWentAway_LetsTheCancellationThrough`
  is the one that stops a client disconnect being reported as a server error. Deleting the four
  lines it guards changes no behaviour any other test can see.

## Architecture

```
            HTTP/1.1 + JSON                     gRPC over plaintext HTTP/2 (h2c)
  client  ──────────────────►  Library.Api  ───────────────────────────────►  Library.Service
                                :5100                                            :5210
                              REST facade                                  rules + EF Core
                                                                                 │
                                                                                 ▼
                                                                          SQLite library.db
```

`Library.Api` owns HTTP only: routing, binding, JSON shapes, OpenAPI, and translating a gRPC status
into an RFC 9457 problem document. It has exactly **one** project reference, to `Library.Contracts`,
so it physically cannot reach the domain or the database. `Library.Service` owns everything else.

```bash
grep -r ProjectReference src/ --include=*.csproj   # Api -> Contracts only; Domain -> nothing
```

**Why two processes.** "Internal service communication" over gRPC only means something across a
process boundary; two projects in one process would not be an honest reading of the brief. The cost is
a second terminal, which the 503 message makes obvious.

**Deployment.** Two containers. The service is not exposed; the API reaches it over h2c on the cluster
network, or through a mesh with mTLS. Both hosts expose a liveness endpoint (`GET /health` on the API,
the gRPC health service on the service) with no dependency checks, so a database blip cannot make an
orchestrator restart a healthy process. Migrations would run as `dotnet ef migrations bundle` in a
deploy job with `Database:MigrateOnStartup` set to false. Configuration is environment variables
through the same keys. No code changes.

## The four questions

Shared rules: a window filters on the borrow date and is **half-open**, `[from, to)` — `from` is
included, `to` is not. Dates are ISO `yyyy-MM-dd`, read as UTC midnight. `limit` defaults to 10 and is
**rejected** above 100 rather than silently clamped. Every ranking ends in `id ascending`, so the order
is total and the top-N is stable.

The sample data is hand-designed so every answer can be checked on paper. These are the answers you
should see on a fresh database.

### Q1 — most borrowed books

`GET /api/books/most-borrowed?limit=5` — ranked by loans, then distinct borrowers, then id.

| Rank | Title | Loans | Distinct borrowers |
| --- | --- | --- | --- |
| 1 | The Hobbit | 6 | 6 |
| 2 | Dune | 6 | 5 |
| 3 | Neuromancer | 3 | 3 |
| 4 | Sapiens | 3 | 3 |
| 5 | Clean Code | 3 | 2 |

The Hobbit and Dune tie on loans, and distinct borrowers breaks it — Dmitri borrowed Dune twice.
Neuromancer and Sapiens tie on both, and the id breaks it. Open loans count as borrow events: the
question is what gets borrowed, not what is on the shelf.

Add `?from=2026-01-01&to=2026-02-01` and Dune drops to **3** loans by 3 borrowers.

### Q2 — top borrowers in a time frame

`GET /api/borrowers/top?from=2026-01-01&to=2026-04-01` — the window is **required** here; without it
the API answers 400 without calling the service.

| Rank | Borrower | Loans | Distinct titles |
| --- | --- | --- | --- |
| 1 | Ava Chen | 6 | 5 |
| 2 | Ben Okafor | 4 | 4 |
| 3 | Dmitri Volkov | 4 | 3 |
| 4 | Clara Diaz | 3 | 3 |
| 5 | Elena Rossi | 3 | 3 |
| 6 | Farid Haddad | 2 | 2 |

Elena borrowed a book at 10:00 on 1 April. It is **excluded** here by the exclusive upper bound; move
`to` to `2026-04-02` and she rises to 4 loans and 4 titles. That is the half-open rule made visible,
and it is why the rule is stated rather than assumed.

### Q3 — reading pace

`GET /api/borrowers/3/reading-pace` — Clara Diaz reads **115.0 pages per day**: 1150 pages over 10
days, from three completed loans (The Hobbit 300/3, Dune 600/6, Neuromancer 250/1). Her open loan is
excluded. The response carries `totalPages` and `totalDays` alongside the figure so the arithmetic is
checkable at a glance, plus a per-loan breakdown.

The brief says "based on the borrow and return duration of a book, assuming continuous reading", which
names its own denominator: the time a book was held is treated as time spent reading it. The judgement
calls on top of that, all of them pinned by unit tests:

| Decision | Choice | Why | Alternative not taken |
| --- | --- | --- | --- |
| Which loans | Completed only | No return date, no measurable duration. An open loan says a member *has* a book, not that they read it | Counting open loans up to now, which understates current readers |
| Duration | Whole days, rounded up, minimum 1 | A librarian's day is a day; integer arithmetic stays hand-checkable; a book returned six hours later cannot report 14,400 pages per day | Fractional days, which is more precise but needs a floor, and the floor needs a defence |
| Aggregation | Σ pages ÷ Σ days | Long books weigh proportionally, and one 30-minute loan cannot wreck the figure | Mean of per-loan paces (outlier-fragile); median (robust, harder to explain) |
| Overlapping loans | Not netted out | The brief's assumption is per book, so a member holding two books is assumed to read both | Union of intervals — see below |
| Nothing to estimate from | `pagesPerDay` is `null` | Absence of data is not a pace of zero | `0.0`, which reads as "reads nothing" |

**The overlap, concretely.** `GET /api/borrowers/1/reading-pace?from=2026-01-01&to=2026-01-12` gives
Ava **82.0**: 820 pages over 4 + 6 days. Her two loans overlap between 4 and 6 January, so merging
them into a single interval of 8 days would give **102.5** instead. That number answers a different
question — pages consumed per calendar day — and both are defensible. This one matches what the brief
describes, and the other is written down here rather than left implicit.

### Q4 — also borrowed

`GET /api/books/2/also-borrowed?limit=3` — five people have borrowed Dune (`cohortSize: 5`), and this
is what else they borrowed, ranked by shared borrowers, then loans by the cohort, then id.

| Rank | Title | Shared borrowers | Loans by cohort |
| --- | --- | --- | --- |
| 1 | The Hobbit | 5 | 5 |
| 2 | Neuromancer | 3 | 3 |
| 3 | Sapiens | 3 | 3 |

Shared borrowers outranks loan count deliberately: further down the list Clean Code has two loans by
the cohort but only one member of it (Ava, twice), and one person reading a book twice is weaker
evidence than two people each reading it once.

A window narrows the ranked list but **not** the cohort: the cohort is everyone who has ever
borrowed the title, and the list is what that group borrowed during the window. Windowing both
would answer a narrower question — "people who read this title *in this window*" — which on any
short window collapses to a handful of readers and a list too sparse to rank. So `cohortSize` is an
all-time figure even when the list beside it is not, and that is stated here rather than left to be
inferred from a surprising number.

`cohortSize` is returned because "3 people also borrowed this" means something very different when the
cohort is 5 than when it is 500. This is raw co-occurrence, so it has a popularity bias: every
borrower of Dune also borrowed The Hobbit, which is simply the most borrowed title in the library.
Correcting that means lift, `P(B|A) ÷ P(B)`, with a minimum-support floor — about fifteen lines over
data the query already reads. It is named in "What I would do next" rather than slipped in, because a
metric nobody can check by hand is worse than a simple number with its bias stated.

## API reference

400, 503 and 504 apply to every `/api` route: any route can be handed a bad value, fail to reach the
service, or time out waiting for it.

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
| `GET` | `/api/books/most-borrowed?from&to&limit` | 200 | — |
| `GET` | `/api/borrowers/top?from&to&limit` | 200 | — |
| `GET` | `/api/borrowers/{id}/reading-pace?from&to` | 200 | 404 |
| `GET` | `/api/books/{id}/also-borrowed?from&to&limit` | 200 | 404 |
| `GET` | `/health` | 200 | — |

Every failure is an RFC 9457 problem document, including a 404 from an unmatched route and a 415
from a `POST` that arrives without a `Content-Type`.

| HTTP | gRPC status | When |
| --- | --- | --- |
| 400 | `InvalidArgument` | Input that can never be valid: blank title, zero pages, `limit` over the cap, inverted window, missing Q2 window |
| 404 | `NotFound` | The book, borrower or loan does not exist |
| 409 | `FailedPrecondition` | Valid request, incompatible world: every copy out, at the loan limit, already holds the title, already returned, duplicate email, lost a race |
| 499 | `Cancelled` | The client went away; logged at Information, not Error |
| 503 | `Unavailable` | The service is unreachable; the detail names the endpoint and the command that starts it |
| 504 | `DeadlineExceeded` | The service did not answer within the 10-second deadline |
| 500 | anything else | A defect; the cause stays server-side |

The 400/409 split is the one a client acts on: **400 says fix the request, 409 says the same request
may succeed later.**

## Data model

```
Book 1───* BookCopy 1───* Loan *───1 Borrower
Book 1───────────────────* Loan          (denormalised BookId, immutable)
```

`Loan.ReturnedAt IS NULL` **is** the state machine — there is no status enum that can drift out of
step with it. `Loan.BookId` is denormalised because every report groups by title, and it is safe
because a copy belongs to one title for life.

Two rules live in the schema rather than in an `if`, because an `if` in C# does not close a
check-then-act race:

```sql
CREATE UNIQUE INDEX UX_Loans_OpenLoanPerCopy        ON Loans (BookCopyId)          WHERE ReturnedAt IS NULL;
CREATE UNIQUE INDEX UX_Loans_OpenTitlePerBorrower   ON Loans (BorrowerId, BookId)  WHERE ReturnedAt IS NULL;
```

Two people borrowing the last copy both see it free; the loser's insert hits the first index and
becomes a 409 telling them to try again. Returning the same loan twice is closed differently:
`ReturnedAt` is a concurrency token, so the second update runs `WHERE Id = @id AND ReturnedAt IS NULL`,
affects zero rows, and becomes the same 409. Both races have a staged integration test that drives
the race through `LendingService`, so what is asserted is the 409 a caller would actually receive
rather than just the index doing its job.

Both mechanisms exist unchanged in SQL Server (`CREATE UNIQUE INDEX ... WHERE ReturnedAt IS NULL`, and
`rowversion` or the same nullable-column token) and in MongoDB (`partialFilterExpression`). The design
is not a SQLite trick.

The concurrent-loan limit is the one rule with no database backstop: two simultaneous borrows can
leave a member one over the limit. That is accepted, bounded and benign, and it is noted in the code
where it applies — closing it costs a serialisable transaction on every borrow.

## Design decisions

**SQL, not MongoDB.** The four questions are joins and group-bys over one fact table, which is
relational work. Mongo would answer them with the aggregation pipeline and would close the last-copy
race the same way with a partial unique index, so it is a legitimate choice; SQL is the more natural
one for these questions.

**SQLite, and what changes for SQL Server.** The brief asks for tests that are easy to execute and
allows a simulated connection; a Docker dependency is the most likely reason a reviewer bounces off a
repository, so `git clone && dotnet test` needs nothing but the SDK. Nothing is simulated: this is a
real engine with real transactions and real partial unique indexes, which is the mechanism the
concurrency story rests on. Every query is provider-agnostic LINQ. Moving to SQL Server is
`UseSqlServer`, a regenerated migration, and the quoting on two index filters (`[ReturnedAt] IS NULL`).
I would not run SQLite in production for this.

**Where the domain model is, and where DDD stops.** Entities have private setters and static
factories and protect their own invariants (`Loan.Return` refuses a second return). The borrow
decision spans several rows, so it is a pure function, `LendingRules.Borrow`, that takes the facts and
decides — testable without a database, and the reason the application service contains no rule of its
own. `ReadingPaceCalculator` is a pure calculator, `DateRange` is a value type, and exceptions are the
domain's failure vocabulary. Aggregates, repositories, CQRS and domain events are what I would add
when the write model grows; at two application services they are ceremony, and `DbContext` is already
a unit of work. The natural place domain events would earn their keep is maintaining rollups.

**Exceptions, not `Result<T>`.** Exceptions are roughly two orders of magnitude slower on the failure
path, which is immaterial at realistic rates. The decision was made on the boundary: one interceptor
cannot be forgotten, whereas a dozen match sites can, and the happy path throws nothing.

**Other calls, briefly.** EF Core migrations, so the schema is reviewable and versioned. `int` ids,
because they are readable in URLs and README examples and there is one writer. Hand-written mapping
between protobuf, the domain and public JSON, because a compile error beats a silently-null property
and the public JSON contract should be able to evolve separately. `TimeProvider` everywhere, so tests
freeze the clock instead of sleeping — `grep -rnE "(DateTime|DateTimeOffset)\.(UtcNow|Now)" src/ --include=*.cs`
returns nothing. (The pattern is anchored to the type on purpose: a bare `UtcNow` also matches
`TimeProvider.GetUtcNow()`, which is the call you *want* to find.) xUnit with Shouldly and NSubstitute, because the brief names either framework and the runner
is not what is being assessed. No retries, because `BorrowBook` is not idempotent and a retry after a
timeout can lend two copies for one request; idempotency keys are the prerequisite.

A fuller log, one paragraph per decision with the alternative and the cost, is in
[`docs/DECISIONS.md`](docs/DECISIONS.md).

## Scope and time

Time spent on this submission: about 10 hours, including the warm-ups, the tests and this README.

Deliberately **not** built, each because it costs more to defend than it adds: authentication and TLS
(out of scope per the brief), Docker and Testcontainers, catalogue paging and search, ISBN/genre/year
fields, a `Result<T>` pipeline, MediatR/CQRS/repositories/AutoMapper, retries and circuit breakers,
OpenTelemetry exporters, and the gRPC rich error model. Each would be the right call at a different
size; none of them answers a question this brief asks.

## What I would do next

1. **Lift instead of raw co-occurrence in Q4**, with a minimum-support floor, to correct the
   popularity bias the sample data makes visible.
2. **Overdue analytics.** `DueAt` is modelled and nothing reads it: overdue rate, mean days late,
   worst titles, repeat late returners.
3. **Utilisation and dead stock** — days on loan ÷ (copies × days in catalogue), and titles with no
   loans in N months. The inverse of Q1 and usually the more actionable list.
4. **Retry the borrow on a lost race.** When the unique index rejects an insert, a second copy may
   still be free; a bounded internal retry turns that 409 into a 201. About five lines.
5. **Rollups maintained by `LoanCreated` and `LoanReturned` events** once live aggregates over a
   growing loan table stop being cheap. The analytics service is already a separate contract, so it
   can move to a read replica without the API noticing.
