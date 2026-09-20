# Test guide

What each tier tests, how it is wired, where a new test belongs. Counts and coverage figures live in
the [README](../README.md#tests), so they only have to be right in one place.

## Run it

```bash
dotnet test                                      # everything; no Docker, no setup
dotnet test tests/Library.UnitTests              # one tier
dotnet test --filter "FullyQualifiedName~LastCopyRace"
dotnet test --collect:"XPlat Code Coverage" --settings coverage.runsettings
```

No fixed ports, no running host needed: every tier starts what it needs. CI runs the same command in
Release.

## The five tiers

| Project | Runs against | Catches only here |
| --- | --- | --- |
| `Library.UnitTests` | Pure code, no I/O | Wrong arithmetic, rule or mapping; a project reference that should not exist |
| `Library.IntegrationTests` | Application services on real SQLite, built by the migration | Untranslatable SQL, work done in memory, constraints, races, a lost `DateTimeKind` |
| `Library.FunctionalTests` | Each host alone: the service in-memory, the API with the service stubbed | Proto mapping, status codes, JSON shape, binding, a setting wired to nothing |
| `Library.SystemTests` | Both hosts on loopback sockets, over HTTP | h2c between two Kestrel hosts; the whole path |
| `Library.Warmups.Tests` | Four pure functions | `int.MinValue`, surrogate pairs, combining marks, overflow |

Cumulative in cost, not in coverage: each tier exists for failures the cheaper ones cannot see.

## Unit — `tests/Library.UnitTests`

Plain objects. No fixture, no database; NSubstitute only for a gRPC call context.

| File | Tests |
| --- | --- |
| `Domain/BookTests` | `Book.Create` bounds on title, author, `pageCount` and `copies`; trimming; copies born with the book |
| `Domain/BorrowerTests` | Display-name form reduced to the address, lower-casing, the BCL parser's permissiveness pinned |
| `Domain/LoanTests` | `Loan.Return` refuses a second return, and a return before the borrow |
| `Domain/LendingRulesTests` | The borrow decision, including which rule wins when two are broken at once |
| `Domain/ReadingPaceCalculatorTests` | Whole-day rounding, overlaps not netted, breakdown order |
| `Domain/DateRangeTests` | Hand-written equality and `GetHashCode`, inverted bounds, `IsBounded` |
| `Service/ProtoMappingTests` | Domain ↔ protobuf, especially unset optionals staying unset |
| `Service/DomainExceptionInterceptorTests` | Exception → gRPC status, no message leaked, a disconnect staying a cancellation |
| `Api/DeadlineInterceptorTests` | The deadline is attached and a caller's own is left alone — asserted on the call context, so no server is needed |
| `Architecture/DependencyRuleTests` | API cannot reach domain or database; domain depends on nothing. Read off the compiled manifests, so it proves no type is used either |

`DependencyRuleTests` is the only test that notices a bad project reference; every other tier stays
green.

## Integration — `tests/Library.IntegrationTests`

Real SQLite through `LibraryDatabase`. No mocks, no in-memory provider.

| File | Tests |
| --- | --- |
| `LendingServiceTests` | Borrow, return, availability, the concurrency rules, what each database failure becomes |
| `AnalyticsServiceTests` | The four reports: ranking, tie-breaks, windows, limits, empty cohorts |
| `ReportSqlTests` | Each report is one grouped statement |
| `DemoDataTests` | The fixture matches the published ids, seeds idempotently, obeys its own constraints |

## Functional — `tests/Library.FunctionalTests`

Each host for real, in isolation, so the other side's failures are trivial to provoke.

| File | Tests |
| --- | --- |
| `Service/LendingFeatureTests`, `Service/AnalyticsFeatureTests` | Every feature through the generated gRPC client, and the status each failure produces — so a Q4 defect fails as a Q4 test |
| `Api/ErrorContractTests` | The gRPC → HTTP status matrix as problem documents; unreachable service, timeouts, 404s, unmatched routes; no cause leaked |
| `Api/RequestContractTests` | Request shape and returned JSON: unset pace as `null` not `0`, UTC-midnight windows, rounding, resolvable `Location` headers |
| `Api/BindingContractTests` | The four things MVC binding decides for us — below |
| `Api/OpenApiDocumentTests` | The document matches the README's route table, in every environment |
| `Hosts/HostConfigurationTests` | Composition: a bad setting stops the host at start-up, and the shipped `appsettings.json` binds and validates |

What `BindingContractTests` pins — none of it our rule, all of it different under minimal APIs:

- `POST` with no `Content-Type` is **415**, not 400: no formatter claims the request, so MVC stops
  before validation.
- `?from=` empty is **absent**, not bad input.
- A repeated parameter binds the **first** value.
- Malformed JSON gives `ValidationProblemDetails`, echoing the JSON reader's message and byte offsets.
- Request DTO strings are **nullable on purpose**: a non-nullable one gains an implicit `[Required]`
  and MVC answers 400 before the service's rule runs (decision 21).

## System — `tests/Library.SystemTests`

Both hosts on real sockets. The service gets the frozen clock; the API keeps the real one, since its
only use of a clock is the gRPC deadline.

| File | Collection | Tests |
| --- | --- | --- |
| `AnalyticsJourneyTests` | `LibrarySystemCollection` | The four questions over HTTP against the published answers; the exclusive upper bound end to end; health on both hosts |
| `LendingJourneyTests` | `LibraryWriteCollection` | Borrow → advance the clock → return → the reading pace moves; the only copy lent once; a new co-borrowing pattern reaching Q4 |

Two collections, so writing journeys get their own host pair and database.

## Warm-ups — `tests/Library.Warmups.Tests`

`BookTitlesTests` and `BookIdsTests`: reversal that keeps a surrogate pair and an accent intact,
repetition that overflows rather than exhausting memory, power-of-two checks agreeing with the BCL.

## The harness

| Type | What it does |
| --- | --- |
| `LibraryDatabase` | One `:memory:` connection with the migration applied and shared across contexts, so every test proves the migration applies to an empty database. `Seeded()` or `Empty()`; carries a clock frozen at 2026-06-15 10:00 UTC, the policy defaults and a `CommandCounter` |
| `CommandCounter` | `DbCommandInterceptor` counting round trips and keeping the SQL — how `ReportSqlTests` catches a report that aggregates in memory and still returns the right answer on 28 loans |
| `StageTheRace` | `SaveChangesInterceptor` firing a competing write once, between the read and the write — the only interleaving that reaches a unique index rather than the `if` in front of it. Forced, not waited for |
| `ServiceHostFixture` | `WebApplicationFactory<ServiceHost>` on a throwaway `.db`, fake clock via `Replace`, plus a handler copying the request's HTTP version so gRPC accepts `TestServer`'s response |
| `ApiHostFixture` | `WebApplicationFactory<ApiHost>` in Production with both gRPC clients substituted; `Returns<T>` and `Fails<T>` provoke any status without a service |
| `LibrarySystem` | `IAsyncLifetime` fixture starting both hosts on `http://127.0.0.1:0` and wiring the API to the service's assigned address |

## Conventions

- Naming is `Subject_Situation_Expectation`; a few read as sentences where that is clearer. The name
  should say what broke without opening the file.
- xUnit v2, Shouldly, NSubstitute; `Xunit` and `Shouldly` are implicit usings. Boolean and count
  assertions carry a `because` string.
- No wall-clock time, no sleeps: `FakeTimeProvider` and `Advance`.
  `grep -rnE "(DateTime|DateTimeOffset)\.(UtcNow|Now)" src/` returns nothing.
- No test waits on a race. Interleavings are staged with an interceptor.
- The three projects referencing `Library.Api` set `LangVersion latest`, inheriting its OpenAPI
  generator's C# 12 floor; the other two keep the `11.0` pin (decision 18).

## Where a new test belongs

| If it… | Write it in |
| --- | --- |
| …is arithmetic, a rule, a mapping or a structural claim | Unit |
| …needs SQL translation, a constraint, an index or an interleaving | Integration |
| …is a status code, JSON shape, binding, gRPC contract or setting | Functional |
| …only breaks with both hosts and a real socket | System |

Pick the cheapest tier that can fail for the reason you care about. A test that can only fail for a
reason the code does not control is worth deleting: two `DateRange` tests went that way with
`DateRange.Contains`, replaced by `TheWindowIsHalfOpenInSqlToTheInstant`, which asserts the half-open
rule where it runs and catches `<` changed to `<=`.

## Six worth opening

| Test | Why |
| --- | --- |
| `ReportSqlTests` | The only thing stopping a report aggregating in memory |
| `LastCopyRace_ThroughTheService_BecomesAConflict` | The check-then-act race, staged through `BorrowBookAsync`, so the caller's 409 is what is under test |
| `TheConcurrentLoanLimit_IsTheOneRuleARaceCanStillBeat` | Characterisation: a member ends one **over** the limit, so the accepted gap is executable and closing it turns a test red |
| `Loan_Instants_SurviveTheRoundTripThroughSqlite` | The only test crossing `UtcDateTimeOffsetConverter`; change its `DateTimeKind` and only this goes red |
| `UnaryServerHandler_ClientWentAway_LetsTheCancellationThrough` | Stops a disconnect becoming a server error; the four lines it guards are invisible to every other test |
| `GetAlsoBorrowedBooks_AWindowNarrowsTheListButNotTheCohort` | Q4's cohort is everyone who ever borrowed the title; only the ranked list is windowed |

## Coverage

`coverage.runsettings` includes `[Library.*]` and excludes the test projects, `Library.Contracts`,
migrations and generated code, so the figure describes code that was written. The README lists the
uncovered lines and why each one is uncovered.
