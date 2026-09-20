# Decision log

One entry per decision a reader could reasonably have made differently: what was chosen, what was
rejected, what it costs. Thirty words each.

**1. Two processes, not two projects.** gRPC only means something across a process boundary.
*Instead:* one process, in-memory calls. *Cost:* a second terminal.

**2. SQL, not MongoDB.** Four joins and group-bys over one fact table is relational work.
*Instead:* Mongo's pipeline and partial unique index — equally correct. *Cost:* none.

**3. SQLite as the only provider.** `dotnet test` needs only the SDK; real transactions and partial
indexes leave nothing simulated. *Instead:* SQL Server in Docker. *Cost:* the port is untested.

**4. EF migrations, not `EnsureCreated`.** The schema is versioned, reviewable, and applied to an
empty database by every integration test. *Cost:* regenerate on model change.

**5. `Book` + `BookCopy`, not a `TotalCopies` column.** A copy with identity makes "one open loan per
copy" a unique index. *Instead:* an integer, where no race can be closed.

**6. `int` identity keys.** Readable in URLs and examples; one writer. *Instead:* Guid v7, for
pre-save ids or many writers. *Cost:* guessable ids, harmless without authorisation.

**7. `Loan.BookId` denormalised.** EF cannot aggregate through a join inside `GroupBy`; this keeps
each report one statement. *Cost:* a column that could drift — set once, never updated.

**8. Lending rules in the domain, as a pure function.** The borrow decision spans rows, so it fits no
entity and needs no database to test. *Cost:* facts read eagerly.

**9. Reading pace in whole days, rounded up, minimum one.** Integers stay hand-checkable; the minimum
stops absurd same-day rates. *Instead:* fractional days, needing a defensible floor.

**10. Overlapping loans are not netted out.** The brief says continuous reading of *a book*.
*Instead:* merged intervals — Ava's January is 82.0 here, 102.5 merged. Both published.

**11. Half-open `[from, to)` windows, UTC midnight.** Intervals compose without double-counting
boundaries. *Cost:* `to=2026-04-01` excludes 1 April, which surprises people; the 400 says so.

**12. `limit` above the cap is rejected, not clamped.** Asking for 500 and silently receiving 100 is
wrong data with no signal. *Cost:* one more failure mode to handle.

**13. Exceptions, not `Result<T>`.** One interceptor and one handler cannot be forgotten; a dozen
match sites can. *Cost:* failures are invisible in method signatures.

**14. No retries between API and service.** `BorrowBook` is not idempotent; a retry can lend two
copies. *Instead:* a per-call deadline, surfacing a wedged service as 504.

**15. xUnit v2, Shouldly, NSubstitute.** The brief allows either framework; the runner is not what is
assessed. *Instead:* NUnit on the Testing Platform — a day of conversion nobody sees.

**16. OpenAPI and Scalar in every environment.** Security is out of scope, and a published build
still serves `/docs`. *Cost:* reachable where authentication would want it.

**17. MVC controllers over Minimal APIs.** `[ApiController]` puts each response contract above its
action. *Cost:* ~60 lines, and `AddApplicationPart`, since MVC otherwise scans the test runner.

**18. Explicit syntax, enforced by `LangVersion 11.0`.** Sugar is a build error, not a review
comment. `Library.Api` needs C# 12 for its OpenAPI generator. *Cost:* DTOs lose value equality.

**19. Email stored as `MailAddress` parsed it, lower-cased.** Storing the input would hide a
duplicate behind a display name or a capital letter. *Cost:* the original casing is gone.

**20. `pageCount` capped at 50,000.** Checked `int` addition in the pace calculator would otherwise
overflow into an unrelated 500. *Instead:* `long` accumulators, if books were ever longer.

**21. Request DTO strings are nullable, deliberately.** A non-nullable one gains an implicit
`[Required]`, so MVC answers 400 and the service's own validation never runs.

**22. The health check registers one always-passing check.** `AddGrpcHealthChecks()` alone answers
`UNKNOWN`, which `grpc_health_probe` fails. Liveness only; a database probe would restart healthy
processes.

---

Several entries carry more reasoning than thirty words hold — the source-generator floor behind 18,
the uniqueness hole behind 19, the other three binding behaviours around 21. It is pinned by tests
rather than prose: [`docs/TESTING.md`](TESTING.md) says which ones and why.
