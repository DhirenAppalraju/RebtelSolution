using Library.Domain.Entities;
using Library.Domain.Exceptions;
using Library.Service.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests
{
    public class LendingServiceTests
    {
        private const int Hobbit = 1;
        private const int CleanCode = 5;
        private const int Refactoring = 6;
        private const int ThinkingFastAndSlow = 8;
        private const int ProjectHailMary = 7;
        private const int OpenLoanOnThinking = 26;

        [Fact]
        public async Task ListBooks_ReportsAvailabilityFromOpenLoansNotTheClock()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var books = await database.NewLendingService(db).ListBooksAsync(default);

                    books.Count.ShouldBe(10);
                    books.Select(b => b.Id).ShouldBe(Enumerable.Range(1, 10));
                    books.Single(b => b.Id == ThinkingFastAndSlow).AvailableCopies.ShouldBe(0);
                    books.Single(b => b.Id == ThinkingFastAndSlow).TotalCopies.ShouldBe(1);
                    books.Single(b => b.Id == ProjectHailMary).AvailableCopies.ShouldBe(1);
                    books.Single(b => b.Id == ProjectHailMary).TotalCopies.ShouldBe(2);
                    books.Single(b => b.Id == Hobbit).AvailableCopies.ShouldBe(3);
                }
            }
        }

        [Fact]
        public async Task BorrowBook_FreeCopy_OpensALoanDueTheConfiguredPeriodLater()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var loan = await database.NewLendingService(db).BorrowBookAsync(1, Refactoring, default);

                    loan.BookTitle.ShouldBe("Refactoring");
                    loan.BorrowerName.ShouldBe("Ava Chen");
                    loan.BorrowedAt.ShouldBe(database.Clock.GetUtcNow());
                    loan.DueAt.ShouldBe(database.Clock.GetUtcNow().AddDays(database.Policy.LoanPeriodDays));
                    loan.ReturnedAt.ShouldBeNull();
                }
            }
        }

        [Fact]
        public async Task BorrowBook_EveryCopyOnLoan_IsAConflict()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    // One copy, and loan 26 still holds it.
                    var error = await Should.ThrowAsync<ConflictException>(() =>
                        database.NewLendingService(db).BorrowBookAsync(1, ThinkingFastAndSlow, default));

                    error.Message.ShouldContain("Every copy");
                }
            }
        }

        [Fact]
        public async Task BorrowBook_SecondCopyOfATitleAlreadyHeld_IsAConflict()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var lending = database.NewLendingService(db);
                    await lending.BorrowBookAsync(1, Hobbit, default);

                    var error = await Should.ThrowAsync<ConflictException>(() => lending.BorrowBookAsync(1, Hobbit, default));

                    error.Message.ShouldContain("already holds a copy");
                }
            }
        }

        [Fact]
        public async Task BorrowBook_AtTheConcurrentLimit_IsAConflict()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var lending = database.NewLendingService(db);

                    foreach (var bookId in new int[] { 1, 2, 3, 4, 5 })
                    {
                        await lending.BorrowBookAsync(2, bookId, default);
                    }

                    var error = await Should.ThrowAsync<ConflictException>(() => lending.BorrowBookAsync(2, Refactoring, default));

                    error.Message.ShouldContain("the limit is 5");
                }
            }
        }

        [Fact]
        public async Task BorrowBook_UnknownBorrowerOrBook_IsNotFound()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var lending = database.NewLendingService(db);

                    await Should.ThrowAsync<NotFoundException>(() => lending.BorrowBookAsync(999, Hobbit, default));
                    await Should.ThrowAsync<NotFoundException>(() => lending.BorrowBookAsync(1, 999, default));
                }
            }
        }

        [Fact]
        public async Task ReturnBook_OpenLoan_RecordsTheClockAndFreesTheCopy()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var lending = database.NewLendingService(db);

                    var returned = await lending.ReturnBookAsync(OpenLoanOnThinking, default);

                    returned.ReturnedAt.ShouldBe(database.Clock.GetUtcNow());
                    (await lending.GetBookAsync(ThinkingFastAndSlow, default)).AvailableCopies.ShouldBe(1);
                }
            }
        }

        [Fact]
        public async Task ReturnBook_Twice_IsAConflict()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var lending = database.NewLendingService(db);
                    await lending.ReturnBookAsync(OpenLoanOnThinking, default);

                    await Should.ThrowAsync<ConflictException>(() => lending.ReturnBookAsync(OpenLoanOnThinking, default));
                }
            }
        }

        [Fact]
        public async Task AddBorrower_DuplicateEmail_IsAConflictFromTheUniqueIndex()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var lending = database.NewLendingService(db);

                    var error = await Should.ThrowAsync<ConflictException>(() =>
                        lending.AddBorrowerAsync("Impostor", "ava.chen@example.com", default));

                    error.Message.ShouldContain("email");
                }
            }
        }

        [Fact]
        public async Task AddBook_CreatesTheCopiesWithIt()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var lending = database.NewLendingService(db);

                    var book = await lending.AddBookAsync("A New Title", "An Author", 123, 4, default);

                    book.TotalCopies.ShouldBe(4);
                    (await lending.GetBookAsync(book.Id, default)).AvailableCopies.ShouldBe(4);
                }
            }
        }

        [Fact]
        public async Task Get_UnknownIds_AreNotFound()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var lending = database.NewLendingService(db);

                    await Should.ThrowAsync<NotFoundException>(() => lending.GetBookAsync(999, default));
                    await Should.ThrowAsync<NotFoundException>(() => lending.GetBorrowerAsync(999, default));
                    await Should.ThrowAsync<NotFoundException>(() => lending.GetLoanAsync(999, default));
                }
            }
        }

        [Fact]
        public async Task Loan_Instants_SurviveTheRoundTripThroughSqlite()
        {
            // The only assertion that crosses the value converter: writes, drops the context, reads
            // back. If the converter stopped saying UtcDateTimeKind, every instant would shift by the
            // local offset and the rest of the suite, which compares differences, would stay green.
            using (var database = LibraryDatabase.Seeded())
            {
                var borrowedAt = database.Clock.GetUtcNow();
                int loanId;

                await using (var writing = database.NewContext())
                {
                    loanId = (await database.NewLendingService(writing)
                        .BorrowBookAsync(1, Refactoring, default)).Id;
                }

                await using (var reading = database.NewContext())
                {
                    var loan = await database.NewLendingService(reading).GetLoanAsync(loanId, default);

                    loan.BorrowedAt.ShouldBe(borrowedAt);
                    loan.BorrowedAt.Offset.ShouldBe(TimeSpan.Zero);
                    loan.DueAt.ShouldBe(borrowedAt.AddDays(database.Policy.LoanPeriodDays));
                    loan.ReturnedAt.ShouldBeNull();
                }

                database.Clock.Advance(TimeSpan.FromHours(30));
                var returnedAt = database.Clock.GetUtcNow();

                await using (var returning = database.NewContext())
                {
                    await database.NewLendingService(returning).ReturnBookAsync(loanId, default);
                }

                await using (var reading = database.NewContext())
                {
                    var loan = await database.NewLendingService(reading).GetLoanAsync(loanId, default);

                    loan.ReturnedAt.ShouldBe(returnedAt);
                    loan.ReturnedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
                }
            }
        }

        [Fact]
        public async Task TheConcurrentLoanLimit_IsTheOneRuleARaceCanStillBeat()
        {
            // Characterisation, not a bug report: the documented, accepted gap. The limit is checked
            // in C# with no index behind it, so simultaneous borrows of different titles both insert.
            // Closing it costs a serialisable transaction per borrow; noted so a fix gets noticed.
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var setup = database.NewContext())
                {
                    var lending = database.NewLendingService(setup);

                    foreach (var bookId in new int[] { 1, 2, 3, 4 })
                    {
                        await lending.BorrowBookAsync(2, bookId, default);
                    }
                }

                await using (var winner = database.NewContext())
                {
                    var now = database.Clock.GetUtcNow();
                    var copyId = await winner.BookCopies
                        .Where(c => c.BookId == CleanCode).OrderBy(c => c.Id).Select(c => c.Id).FirstAsync();

                    // Fifth loan lands between the count of four and the insert.
                    var fifth = new StageTheRace(async () =>
                    {
                        winner.Loans.Add(Loan.Open(copyId, CleanCode, 2, now, now.AddDays(21)));
                        await winner.SaveChangesAsync();
                    });

                    await using (var loser = database.NewContext(fifth))
                    {
                        // No exception: different title, so no index applies.
                        await database.NewLendingService(loser).BorrowBookAsync(2, Refactoring, default);

                        fifth.Fired.ShouldBeTrue();
                    }
                }

                await using (var db = database.NewContext())
                {
                    var open = await db.Loans.CountAsync(l => l.BorrowerId == 2 && l.ReturnedAt == null);

                    open.ShouldBe(database.Policy.MaxConcurrentLoans + 1,
                        "the limit is advisory under concurrency; if this now equals the limit, the gap was closed and the README should say so");
                }
            }
        }

        [Fact]
        public async Task AnUnrecognisedUniqueViolation_IsStillAConflictWithAGenericMessage()
        {
            // The fall-through arm: a fourth unique index would hit it and still yield a 409, not a
            // 500. Staged with an index the application does not have, the only way to reach it.
            using (var database = LibraryDatabase.Empty())
            {
                await using (var db = database.NewContext())
                {
                    var lending = database.NewLendingService(db);
                    await lending.AddBookAsync("A Title", "An Author", 100, 1, default);

                    await db.Database.ExecuteSqlRawAsync(
                        "CREATE UNIQUE INDEX UX_Books_Title_Staged ON Books (Title)");

                    var error = await Should.ThrowAsync<ConflictException>(() =>
                        lending.AddBookAsync("A Title", "Another Author", 100, 1, default));

                    error.Message.ShouldBe("The change conflicts with a record that already exists.");
                }
            }
        }

        [Fact]
        public async Task LastCopyRace_IsClosedByTheIndexNotByAnIf()
        {
            // Staged, not parallel: timing-based race tests pass or fail on timing.
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var first = database.NewContext())
                {
                    await using (var second = database.NewContext())
                    {
                        var copyId = await first.BookCopies.Where(c => c.BookId == Refactoring).Select(c => c.Id).SingleAsync();
                        var now = database.Clock.GetUtcNow();

                        // Both saw the copy free before either wrote.
                        (await first.Loans.AnyAsync(l => l.BookCopyId == copyId && l.ReturnedAt == null)).ShouldBeFalse();
                        (await second.Loans.AnyAsync(l => l.BookCopyId == copyId && l.ReturnedAt == null)).ShouldBeFalse();

                        first.Loans.Add(Loan.Open(copyId, Refactoring, 1, now, now.AddDays(21)));
                        await first.SaveChangesAsync();

                        second.Loans.Add(Loan.Open(copyId, Refactoring, 2, now, now.AddDays(21)));
                        var error = await Should.ThrowAsync<DbUpdateException>(() => second.SaveChangesAsync());

                        var conflict = DatabaseErrors.AsConflict(error);
                        conflict.ShouldNotBeNull();
                        conflict.Message.ShouldContain("taken a moment ago");
                    }
                }
            }
        }

        [Fact]
        public async Task LastCopyRace_ThroughTheService_BecomesAConflict()
        {
            // Other half of the test above: that one proves the index fires and DatabaseErrors maps
            // it; this proves the catch in SaveAsync turns the loser into a ConflictException.
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var winner = database.NewContext())
                {
                    var now = database.Clock.GetUtcNow();
                    var copyId = await winner.BookCopies
                        .Where(c => c.BookId == Refactoring).Select(c => c.Id).SingleAsync();

                    // Between the "copy is free" read and the insert.
                    var stolen = new StageTheRace(async () =>
                    {
                        winner.Loans.Add(Loan.Open(copyId, Refactoring, 1, now, now.AddDays(21)));
                        await winner.SaveChangesAsync();
                    });

                    await using (var loser = database.NewContext(stolen))
                    {
                        var error = await Should.ThrowAsync<ConflictException>(() =>
                            database.NewLendingService(loser).BorrowBookAsync(2, Refactoring, default));

                        stolen.Fired.ShouldBeTrue("the competing write must land inside the call, or this proves nothing");
                        error.Message.ShouldContain("taken a moment ago");
                    }
                }
            }
        }

        [Fact]
        public async Task OpenTitleRace_ThroughTheService_BecomesAConflict()
        {
            // Same staging, other index: competing write takes a different copy of the same title,
            // so the loser passes the copy index and fails (BorrowerId, BookId).
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var winner = database.NewContext())
                {
                    var now = database.Clock.GetUtcNow();
                    var copies = await winner.BookCopies
                        .Where(c => c.BookId == Hobbit).OrderBy(c => c.Id).Select(c => c.Id).ToListAsync();

                    var stolen = new StageTheRace(async () =>
                    {
                        winner.Loans.Add(Loan.Open(copies[1], Hobbit, 5, now, now.AddDays(21)));
                        await winner.SaveChangesAsync();
                    });

                    await using (var loser = database.NewContext(stolen))
                    {
                        var error = await Should.ThrowAsync<ConflictException>(() =>
                            database.NewLendingService(loser).BorrowBookAsync(5, Hobbit, default));

                        stolen.Fired.ShouldBeTrue();
                        error.Message.ShouldContain("already holds a copy");
                    }
                }
            }
        }

        [Fact]
        public async Task NonConflictDatabaseFailure_IsNotDisguisedAsAConflict()
        {
            // The rethrow in SaveAsync: AsConflict handles unique violations only, so any other
            // failure stays a 500 rather than a 409 telling the caller to retry the impossible.
            using (var database = LibraryDatabase.Empty())
            {
                await using (var saboteur = database.NewContext())
                {
                    var broken = new StageTheRace(async () =>
                    {
                        await saboteur.Database.ExecuteSqlRawAsync("DROP TABLE BookCopies");
                    });

                    await using (var db = database.NewContext(broken))
                    {
                        var error = await Should.ThrowAsync<DbUpdateException>(() =>
                            database.NewLendingService(db).AddBookAsync("A Title", "An Author", 100, 1, default));

                        broken.Fired.ShouldBeTrue();
                        error.ShouldNotBeOfType<ConflictException>();
                    }
                }
            }
        }

        [Fact]
        public async Task OpenTitlePerBorrowerIndex_RejectsASecondCopyOfOneTitle()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var now = database.Clock.GetUtcNow();

                    db.Loans.Add(Loan.Open(2, Hobbit, 1, now, now.AddDays(21)));
                    db.Loans.Add(Loan.Open(3, Hobbit, 1, now, now.AddDays(21)));

                    var error = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());

                    DatabaseErrors.AsConflict(error)!.Message.ShouldContain("already holds a copy");
                }
            }
        }

        [Fact]
        public async Task DoubleReturnRace_IsClosedByTheConcurrencyToken()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var first = database.NewContext())
                {
                    await using (var second = database.NewContext())
                    {
                        // Loser loaded before the winner committed: its copy is still open.
                        var stale = await second.Loans.SingleAsync(l => l.Id == OpenLoanOnThinking);
                        stale.IsOpen.ShouldBeTrue();

                        await database.NewLendingService(first).ReturnBookAsync(OpenLoanOnThinking, default);

                        // UPDATE ... WHERE ReturnedAt IS NULL affects zero rows.
                        await Should.ThrowAsync<ConflictException>(() =>
                            database.NewLendingService(second).ReturnBookAsync(OpenLoanOnThinking, default));
                    }
                }
            }
        }
    }
}
