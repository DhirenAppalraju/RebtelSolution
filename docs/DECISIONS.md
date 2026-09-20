# Decision log

One entry per decision that a reader could reasonably have made differently. Each names the
alternative and what choosing this way costs.

### 1. Two processes, not two projects

The brief lists gRPC as mandatory for "internal service communication". That phrase only means
something across a process boundary, so `Library.Api` and `Library.Service` are separate hosts talking
over plaintext HTTP/2. The alternative — two projects in one process with an in-memory call — would
have been cheaper to run and would not have demonstrated the thing being asked for. The cost is a
second terminal, paid down by a 503 message that names the command to start the missing host.

### 2. SQL over MongoDB

Four questions that are joins and group-bys over one `Loans` fact table are relational work. Mongo
would answer them with the aggregation pipeline and would close the last-copy race the same way, with
a partial unique index, so this is a preference rather than a correctness argument. Cost: none that
this brief exposes.

### 3. SQLite as the only provider

Chosen so that `git clone && dotnet test` needs nothing but the SDK, which is what "tests easy to
execute" asks for. It is a real engine — real transactions, real partial unique indexes — so nothing
about the concurrency design is simulated. The alternative, SQL Server in Docker, is more
production-realistic and is the single most likely reason a reviewer never gets the tests running.
Cost: a reviewer has to take on trust that the design ports, which is why the README spells out
exactly what changes (`UseSqlServer`, a regenerated migration, the quoting on two index filters).

### 4. EF Core migrations rather than `EnsureCreated`

The schema is reviewable in source and versioned, and the integration tests build their database from
the same migration that a deployment would apply — so "the migration applies cleanly to an empty
database" is proven by every integration test rather than by one. Cost: a migrations folder to
regenerate when the model changes.

### 5. `Book` + `BookCopy`, not a `TotalCopies` column

Lending is about physical items. Modelling the copy gives each one an identity, which is what makes
"one open loan per copy" expressible as a unique index. A `TotalCopies` integer would have forced the
last-copy check into application code, where a race cannot be closed. Cost: one more table and a seed
step. `BookCopy` carries nothing but its identity; labels, condition and retirement were left out.

### 6. `int` identity keys

Readable in URLs, in the README's examples and in the sample data, and the conventional EF default.
Guid v7 is the right answer when an entity must be identified before it is saved or when several
writers generate ids; this system has one writer. Cost: ids are guessable, which matters only under an
authorisation model that is out of scope here.

### 7. `Loan.BookId` denormalised

A loan reaches its book through `BookCopy`, but every report groups by title, and EF Core cannot
translate an aggregate whose operand arrives through a join inside a `GroupBy`. Storing `BookId` on
the loan keeps each report a single statement. It is safe because a copy belongs to one title for
life. Cost: a column that could in principle disagree with `BookCopy.BookId`; it is set once, by the
factory, and never updated.

### 8. Lending rules in the domain, as a pure function

The borrow decision depends on several rows, so it cannot live on a single entity, and putting it in
the data-access code would mean it could only ever be tested against a database.
`LendingRules.Borrow(...)` takes the facts — the open-loan count, whether the title is already held,
the first free copy — and decides. The application service gathers facts and persists; it contains no
rule of its own. Cost: the facts are read slightly more eagerly than a hand-tuned query would.

### 9. Reading pace in whole days, rounded up, minimum one

A librarian's day is a day; integer arithmetic keeps the answer hand-checkable (1150 over 10); and the
minimum of one stops a same-day return from reporting an absurd rate. The alternative, fractional
days, is more precise but needs a floor for very short loans, and that floor needs a value and a
defence. Cost: precision on short loans, which the per-loan breakdown in the response makes visible.
If the estimate were feeding a model rather than a librarian, fractional with an explicit floor would
be the better choice.

### 10. Overlapping loans are not netted out

The brief says "assuming continuous reading" of *a book*, so each loan is treated as continuous
reading of that book over its own borrow-to-return period. When a member holds two books at once, both
count. Merging the intervals instead answers "pages per calendar day", a different and equally
defensible question; for Ava's January the two numbers are 82.0 and 102.5. Cost: the headline can
exceed what one person plausibly reads in a day. Both numbers are in the README so the choice is
visible rather than hidden.

### 11. Half-open `[from, to)` windows on `DateOnly`

`from` inclusive, `to` exclusive, dates read as UTC midnight. Half-open intervals compose without
double-counting boundaries, and whole-day inputs are what a caller actually has. The sample data
deliberately contains a loan at 10:00 on 1 April so the rule is observable rather than theoretical.
Cost: `to=2026-04-01` excluding 1 April surprises people, so the API says so in the 400 message and
the README shows the effect.

### 12. `limit` rejected above the cap, not clamped

A caller who asks for 500 and silently receives 100 has been given wrong data with no signal. A 400
that names the cap is information. Cost: one more failure mode for a client to handle. The validation
lives in the service, not the API, so the configurable policy has one source of truth; the API
validates only what only it can see, which is the shape of the date window.

### 13. Exceptions rather than `Result<T>`

Exceptions are roughly two orders of magnitude slower on the failure path, which is immaterial at
realistic rates for this system. The decision was made on the boundary rather than on the benchmark:
one server interceptor and one API exception handler cannot be forgotten, whereas a dozen match sites
can be, and the happy path throws nothing. Cost: failures are invisible in method signatures.

### 14. No retries between the API and the service

`BorrowBook` is not idempotent. A retry after a timeout can lend two copies for one request, which is
worse than the failure it is trying to hide. Idempotency keys are the prerequisite and were not built.
Reads could safely be retried, but a policy that applies to some calls and not others is a footgun at
this size. A per-call deadline is applied instead, so a wedged service surfaces as a 504 rather than
holding API requests open forever.

### 15. xUnit v2, Shouldly, NSubstitute on the standard runner

The brief names either framework, and the test runner is not what is being assessed. The alternative
considered was NUnit on the Microsoft Testing Platform, which would have cost a day's worth of
assertion conversion that a reviewer never sees. That time went into the functional tier instead.
Cost: none visible; moving to xUnit v3 on the testing platform is a small, separate step.

### 16. OpenAPI and Scalar mapped in every environment

The usual pattern hides API documentation outside Development. Security is out of scope by the brief,
the functional tests exercise the document in Production, and a reviewer running a published build
still gets `/docs`. Cost: the document is reachable in a deployment that would want it behind auth —
which is a decision to revisit at the same time as authentication.

### 17. MVC controllers over Minimal APIs

Both were written; the controllers are what shipped. Minimal APIs made the routing table shorter and
gave typed results without attributes, but every route's response contract then lived in a chain of
`.Produces...` calls that only a reader who already knows the pattern can follow. `[ApiController]`
puts the contract in attributes above the action, which is the shape most .NET teams already read
fluently, and the OpenAPI assertions in the functional tier pin it either way. Cost: roughly 60 more
lines across the three controllers, a `MapControllers()` that scans an assembly instead of explicit
registration, and one non-obvious line — `AddApplicationPart` in `ApiHost`, which is needed because
MVC otherwise scans the *entry* assembly, and under a test runner that is the runner.

### 18. Explicit syntax over the newer shorthand, enforced by the compiler

The codebase avoids primary constructors, records, collection expressions, target-typed `new`,
switch expressions, file-scoped namespaces, expression-bodied members and top-level statements, in
favour of the forms those features are sugar for. This is a readability decision, not a
compatibility one: the target is still `net10.0`, and nothing here would fail to compile otherwise.

The reason it is a decision rather than a habit is that **`Directory.Build.props` pins
`LangVersion` to `11.0`**, so a primary constructor or a collection expression is a build error
rather than a code-review comment. That pin covers `Library.Domain`, `Library.Service`,
`Library.Warmups`, `Library.Contracts` and two of the five test projects — about half the tree by
line count, and all of the domain and service layers. The exception is `Library.Api` and the three
test projects that reference it: `Microsoft.AspNetCore.OpenApi`'s XML-comment source generator
emits `file` types and collection expressions, so the compiler rejects *its own generated code*
below C# 12. Those four projects say so in their csproj and rely on `.editorconfig`, which turns
off the IDE suggestions that would otherwise quietly undo the style everywhere.

An earlier draft of this entry claimed the whole solution would build at C# 11. It would not, and
the claim was worth more as something you can run than as something broader that fails when you do.

The cost is real: the DTOs in `Contracts/` and `Application/Models/` are about 200 lines longer as
classes than they were as records, and those classes no longer carry value equality. Nothing
depends on that equality today, which is why the trade was available; a future test that compares
two DTOs directly would need `Equals` written by hand, and should instead compare the fields it
cares about. `DateRange` is the one type where the equality *was* load-bearing, so it is written
out by hand and has its own tests — including that two windows naming the same instants at
different UTC offsets compare equal, which is what the generated record equality did.

### 19. Email is stored as the parser returned it, lower-cased

`Borrower.Create` validates with `MailAddress.TryCreate` — a BCL parser rather than a regex, so
there is no ReDoS surface and no hand-rolled RFC 5322. Two things follow that are easy to get
wrong, and both are pinned by tests.

It stores `address.Address`, not the input. `MailAddress` accepts the display-name form, so
`Ava Chen <ava.chen@example.com>` would otherwise be stored whole — and `UX_Borrowers_Email` would
not see that the address inside it already belongs to somebody. Keeping the input was a
uniqueness hole disguised as a formatting choice.

It lower-cases. SQLite's default collation is `BINARY`, so without normalising,
`Ava.Chen@example.com` and `ava.chen@example.com` are two members. RFC 5321 does make the local
part case-sensitive and no mail provider on earth honours that; two accounts differing only by
case is a support ticket, not a feature. Normalising in the domain rather than with
`.UseCollation("NOCASE")` keeps the rule in one readable place and needs no migration to move
providers. The alternative — case-preserving storage with a separate normalised index column — is
what I would do if the original casing ever had to be displayed back.

The parser is permissive about more than the display-name form: `a@b`, a trailing dot and quoted
local parts are all accepted, and `BorrowerTests` pins that behaviour rather than pretending it is
stricter. That is tolerable precisely because the parsed address is what gets stored. Proving an
address reaches a human is a confirmation email's job, not a regex's.

### 20. `pageCount` has an upper bound, for a reason that is not obvious locally

`Book.Create` caps pages at 50,000, next to the 1–100 bound on `copies`. The `copies` bound is
about write amplification; this one is about arithmetic somewhere else entirely.
`ReadingPaceCalculator` sums pages across a borrower's completed loans with ordinary `int`
addition, which is checked in C#. Two loans of a book with `int.MaxValue` pages are enough to throw
`OverflowException` out of `GET /api/borrowers/{id}/reading-pace` — a 500 on a request that has
nothing to do with the book that was added. Widening the accumulators to `long` would also work
and would be the right call if page counts were ever legitimately large; bounding the input is
cheaper, and a five-figure cap is far above the longest book ever printed.

### 21. What MVC binding decides on the API's behalf

Moving from Minimal APIs to controllers changed four things at the edge that no rule of ours
chose. They are written down and pinned by `BindingContractTests` rather than left to be
discovered by a caller, because "the framework does that" is not an answer if nobody knew.

- **A `POST` with no `Content-Type` is 415, not 400.** No input formatter can claim the request,
  so MVC stops before model validation. 415 is the more accurate answer — the media type is the
  problem, not the contents — so this one is an improvement worth keeping.
- **`?from=` with an empty value is treated as absent, not as bad input.** The binder converts an
  empty string to `null` for a nullable target; the minimal-API binder called `TryParse("")` and
  answered 400. Empty-as-absent is the conventional reading of a query string.
- **A repeated parameter binds the first value.** `?limit=1&limit=2` is `1`, where minimal APIs
  joined the values and failed to parse. Neither is obviously right; first-wins is at least
  predictable.
- **Malformed JSON produces a `ValidationProblemDetails` with an `errors` object**, which the
  minimal-API pipeline could not emit. That is more useful to a client and slightly more
  revealing — it echoes the JSON reader's own message, including byte offsets. Acceptable for an
  API whose audience is a developer; for a public one I would flatten it in
  `InvalidModelStateResponseFactory`.

The related trap is that the request DTOs use **nullable** strings deliberately. Under
`[ApiController]`, a non-nullable reference property would gain an implicit `[Required]`, MVC
would answer 400 itself, and the service's validation — the single place that owns the rule —
would never run. `Post_EmptyJsonObject_ReachesTheServiceAndFailsItsRule` is the test that notices
if somebody tidies the `?` away.

### 22. The service's health check reports SERVING, which took a registration

`AddGrpcHealthChecks()` alone answers `UNKNOWN`, because the overall status of zero registered
checks is unknown rather than healthy — and `grpc_health_probe` treats anything but `SERVING` as
a failure. So the service registers exactly one check that always passes. That is liveness and
nothing more: no database probe, on purpose, so a transient database fault cannot persuade an
orchestrator to restart a process that is running perfectly well. Readiness — which *should*
consider the database — would be a second, separately named check, and is not built because
nothing here deploys.

This was found by writing the test the README's claim implied, which is the argument for writing
that kind of test.
