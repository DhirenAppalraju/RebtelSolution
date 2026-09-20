using Library.Domain.Analytics;
using Library.Domain.Exceptions;

namespace Library.IntegrationTests
{
    public class AnalyticsServiceTests
    {
        private static DateTimeOffset Utc(int month, int day)
        {
            return new DateTimeOffset(2026, month, day, 0, 0, 0, TimeSpan.Zero);
        }

        [Fact]
        public async Task GetMostBorrowedBooks_AllTime_RanksByLoansThenDistinctBorrowersThenId()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var books = await database.NewAnalyticsService(db).GetMostBorrowedBooksAsync(DateRange.All, 5, default);

                    books.Select(b => (b.Title, b.BorrowCount, b.DistinctBorrowers)).ShouldBe(new[]
                    {
                        ("The Hobbit", 6, 6),
                        ("Dune", 6, 5),          // ties with the Hobbit on loans; distinct borrowers breaks it
                        ("Neuromancer", 3, 3),
                        ("Sapiens", 3, 3),       // ties with Neuromancer on both; id breaks it
                        ("Clean Code", 3, 2),
                    });
                }
            }
        }

        [Fact]
        public async Task GetMostBorrowedBooks_JanuaryWindow_NarrowsDuneToThreeLoans()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var books = await database.NewAnalyticsService(db)
                        .GetMostBorrowedBooksAsync(new DateRange(Utc(1, 1), Utc(2, 1)), 10, default);

                    var dune = books.Single(b => b.Title == "Dune");
                    dune.BorrowCount.ShouldBe(3);
                    dune.DistinctBorrowers.ShouldBe(3);
                }
            }
        }

        [Fact]
        public async Task GetMostBorrowedBooks_LimitAboveTheCap_IsRejectedNotClamped()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var error = await Should.ThrowAsync<ValidationException>(() =>
                        database.NewAnalyticsService(db).GetMostBorrowedBooksAsync(DateRange.All, 101, default));

                    error.Message.ShouldContain("100");
                }
            }
        }

        [Fact]
        public async Task GetMostBorrowedBooks_ZeroLimit_UsesTheConfiguredDefault()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var books = await database.NewAnalyticsService(db).GetMostBorrowedBooksAsync(DateRange.All, 0, default);

                    books.Count.ShouldBe(database.Policy.DefaultReportLimit);
                }
            }
        }

        [Fact]
        public async Task GetTopBorrowers_FirstQuarter_RanksByLoansThenDistinctTitlesThenId()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var borrowers = await database.NewAnalyticsService(db)
                        .GetTopBorrowersAsync(new DateRange(Utc(1, 1), Utc(4, 1)), 10, default);

                    borrowers.Select(b => (b.FullName, b.LoanCount, b.DistinctTitles)).ShouldBe(new[]
                    {
                        ("Ava Chen", 6, 5),
                        ("Ben Okafor", 4, 4),     // ties with Dmitri on loans; distinct titles breaks it
                        ("Dmitri Volkov", 4, 3),
                        ("Clara Diaz", 3, 3),     // ties with Elena on both; id breaks it
                        ("Elena Rossi", 3, 3),
                        ("Farid Haddad", 2, 2),
                    });
                }
            }
        }

        [Fact]
        public async Task GetTopBorrowers_ExclusiveUpperBound_DecidesTheFirstAprilLoan()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var analytics = database.NewAnalyticsService(db);

                    var excluded = await analytics.GetTopBorrowersAsync(new DateRange(Utc(1, 1), Utc(4, 1)), 10, default);
                    var included = await analytics.GetTopBorrowersAsync(new DateRange(Utc(1, 1), Utc(4, 2)), 10, default);

                    excluded.Single(b => b.FullName == "Elena Rossi").LoanCount.ShouldBe(3);
                    included.Single(b => b.FullName == "Elena Rossi").LoanCount.ShouldBe(4);
                    included.Single(b => b.FullName == "Elena Rossi").DistinctTitles.ShouldBe(4);
                }
            }
        }

        [Fact]
        public async Task GetTopBorrowers_WithoutAWindow_IsRejected()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    await Should.ThrowAsync<ValidationException>(() =>
                        database.NewAnalyticsService(db).GetTopBorrowersAsync(DateRange.All, 10, default));
                }
            }
        }

        [Fact]
        public async Task GetReadingPace_Clara_Is115PagesPerDayOverThreeCompletedLoans()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var pace = await database.NewAnalyticsService(db).GetReadingPaceAsync(3, DateRange.All, default);

                    pace.FullName.ShouldBe("Clara Diaz");
                    pace.Estimate.PagesPerDay.ShouldBe(115.0);
                    pace.Estimate.CompletedLoans.ShouldBe(3);       // the open loan 28 is excluded
                    pace.Estimate.TotalPages.ShouldBe(1150);
                    pace.Estimate.TotalDays.ShouldBe(10);
                    pace.Estimate.Loans.Select(l => l.PagesPerDay).ShouldBe(new double[] { 100.0, 100.0, 250.0 });
                }
            }
        }

        [Fact]
        public async Task GetReadingPace_JanuaryWindow_DoesNotNetOutOverlappingLoans()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var pace = await database.NewAnalyticsService(db)
                        .GetReadingPaceAsync(1, new DateRange(Utc(1, 1), Utc(1, 12)), default);

                    // Merged intervals would give 820 / 8 = 102.5.
                    pace.Estimate.PagesPerDay.ShouldBe(82.0);
                    pace.Estimate.TotalPages.ShouldBe(820);
                    pace.Estimate.TotalDays.ShouldBe(10);
                }
            }
        }

        [Fact]
        public async Task GetReadingPace_BorrowerWithNoCompletedLoans_HasNothingToEstimateFrom()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var borrower = await database.NewLendingService(db).AddBorrowerAsync("New Member", "new@example.com", default);

                    var pace = await database.NewAnalyticsService(db).GetReadingPaceAsync(borrower.Id, DateRange.All, default);

                    pace.Estimate.PagesPerDay.ShouldBeNull();
                    pace.Estimate.CompletedLoans.ShouldBe(0);
                }
            }
        }

        [Fact]
        public async Task GetReadingPace_UnknownBorrower_IsNotFound()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    await Should.ThrowAsync<NotFoundException>(() =>
                        database.NewAnalyticsService(db).GetReadingPaceAsync(999, DateRange.All, default));
                }
            }
        }

        [Fact]
        public async Task GetAlsoBorrowedBooks_Dune_RanksBySharedBorrowersAndReportsCohortSize()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var result = await database.NewAnalyticsService(db).GetAlsoBorrowedBooksAsync(2, DateRange.All, 3, default);

                    result.Title.ShouldBe("Dune");
                    result.CohortSize.ShouldBe(5);
                    result.Books.Select(b => (b.Title, b.SharedBorrowers, b.LoanCount)).ShouldBe(new[]
                    {
                        ("The Hobbit", 5, 5),
                        ("Neuromancer", 3, 3),
                        ("Sapiens", 3, 3),
                    });
                }
            }
        }

        [Fact]
        public async Task GetAlsoBorrowedBooks_SharedBorrowersOutranksLoanCount()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var result = await database.NewAnalyticsService(db).GetAlsoBorrowedBooksAsync(2, DateRange.All, 10, default);

                    // Clean Code: two cohort loans, one cohort member.
                    var titles = result.Books.Select(b => b.Title).ToList();
                    var cleanCode = result.Books.Single(b => b.Title == "Clean Code");
                    cleanCode.SharedBorrowers.ShouldBe(1);
                    cleanCode.LoanCount.ShouldBe(2);
                    titles.IndexOf("Project Hail Mary").ShouldBeLessThan(titles.IndexOf("Clean Code"));
                    titles.ShouldNotContain("Dune");
                }
            }
        }

        [Fact]
        public async Task GetAlsoBorrowedBooks_NeverBorrowedBook_IsAnEmptyCohortNotAMissingBook()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var book = await database.NewLendingService(db).AddBookAsync("Untouched", "Nobody", 100, 1, default);

                    var result = await database.NewAnalyticsService(db).GetAlsoBorrowedBooksAsync(book.Id, DateRange.All, 5, default);

                    result.CohortSize.ShouldBe(0);
                    result.Books.ShouldBeEmpty();
                }
            }
        }

        [Fact]
        public async Task GetMostBorrowedBooks_NegativeLimit_IsRejected()
        {
            // The API tier asserts this against a stub; this runs the real Resolve branch.
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var error = await Should.ThrowAsync<ValidationException>(() =>
                        database.NewAnalyticsService(db).GetMostBorrowedBooksAsync(DateRange.All, -5, default));

                    error.Message.ShouldContain("negative");
                }
            }
        }

        [Fact]
        public async Task EveryReport_ReturnsAtMostTheRequestedLimit()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var analytics = database.NewAnalyticsService(db);
                    var firstQuarter = new DateRange(Utc(1, 1), Utc(4, 1));

                    (await analytics.GetMostBorrowedBooksAsync(DateRange.All, 2, default)).Count.ShouldBe(2);
                    (await analytics.GetTopBorrowersAsync(firstQuarter, 2, default)).Count.ShouldBe(2);
                    (await analytics.GetAlsoBorrowedBooksAsync(2, DateRange.All, 2, default)).Books.Count.ShouldBe(2);
                }
            }
        }

        [Fact]
        public async Task TheWindowIsHalfOpenInSqlToTheInstant()
        {
            // Loan 12 is the only one at 2026-02-01 10:00: a window starting there includes it,
            // one ending there does not. The half-open rule in the WHERE clause.
            var instant = new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);

            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var analytics = database.NewAnalyticsService(db);

                    var fromTheInstant = await analytics.GetMostBorrowedBooksAsync(
                        new DateRange(instant, Utc(2, 2)), 10, default);

                    var toTheInstant = await analytics.GetMostBorrowedBooksAsync(
                        new DateRange(Utc(1, 31), instant), 10, default);

                    fromTheInstant.Select(b => b.Title).ShouldBe(new[] { "Neuromancer" });
                    toTheInstant.ShouldBeEmpty();
                }
            }
        }

        [Fact]
        public async Task AnOpenEndedWindow_FiltersOnThatBoundAlone()
        {
            // One-sided windows are supported; each bound is its own `if` in QueryExtensions and
            // only both-set and neither-set had been exercised.
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var analytics = database.NewAnalyticsService(db);

                    // Loans 26-28 are the only ones from May onwards.
                    var fromMay = await analytics.GetMostBorrowedBooksAsync(
                        new DateRange(Utc(5, 1), null), 10, default);

                    // Loans 1 and 2 are the only ones before 3 January.
                    var beforeTheThird = await analytics.GetMostBorrowedBooksAsync(
                        new DateRange(null, Utc(1, 3)), 10, default);

                    fromMay.Select(b => b.BookId).ShouldBe(new[] { 7, 8, 10 });
                    fromMay.ShouldAllBe(b => b.BorrowCount == 1);

                    beforeTheThird.Select(b => b.Title).ShouldBe(new[] { "The Hobbit", "Clean Code" });
                }
            }
        }

        [Fact]
        public async Task GetAlsoBorrowedBooks_AWindowNarrowsTheListButNotTheCohort()
        {
            // Cohort is everyone who ever borrowed the title; the ranked list is what that group
            // borrowed in the window. Windowing both would change cohortSize silently.
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var analytics = database.NewAnalyticsService(db);

                    var allTime = await analytics.GetAlsoBorrowedBooksAsync(2, DateRange.All, 10, default);
                    var january = await analytics.GetAlsoBorrowedBooksAsync(
                        2, new DateRange(Utc(1, 1), Utc(2, 1)), 10, default);

                    // Five have borrowed Dune, in every window.
                    allTime.CohortSize.ShouldBe(5);
                    january.CohortSize.ShouldBe(5);

                    // Only the list narrows: three of the five took The Hobbit in January, five all time.
                    allTime.Books[0].ShouldSatisfyAllConditions(
                        b => b.Title.ShouldBe("The Hobbit"),
                        b => b.SharedBorrowers.ShouldBe(5));

                    january.Books[0].ShouldSatisfyAllConditions(
                        b => b.Title.ShouldBe("The Hobbit"),
                        b => b.SharedBorrowers.ShouldBe(3),
                        b => b.LoanCount.ShouldBe(3));
                }
            }
        }

        [Fact]
        public async Task GetAlsoBorrowedBooks_AWindowWithNoLoans_StillReportsTheWholeCohort()
        {
            // Empty list beside a cohort of five is the answer, not a contradiction.
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var result = await database.NewAnalyticsService(db)
                        .GetAlsoBorrowedBooksAsync(2, new DateRange(Utc(7, 1), Utc(8, 1)), 10, default);

                    result.CohortSize.ShouldBe(5);
                    result.Books.ShouldBeEmpty();
                }
            }
        }

        [Fact]
        public async Task GetAlsoBorrowedBooks_UnknownBook_IsNotFound()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    await Should.ThrowAsync<NotFoundException>(() =>
                        database.NewAnalyticsService(db).GetAlsoBorrowedBooksAsync(999, DateRange.All, 5, default));
                }
            }
        }
    }
}
