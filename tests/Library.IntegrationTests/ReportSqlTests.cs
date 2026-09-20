using Library.Domain.Analytics;

namespace Library.IntegrationTests
{
    /// <summary>
    /// Enforces "the work happens in the database": in-memory aggregation still returns the
    /// right answer on 28 loans, and fails here.
    /// </summary>
    public class ReportSqlTests
    {
        private static readonly DateRange FirstQuarter = new DateRange(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));

        [Fact]
        public async Task MostBorrowedBooks_IsOneGroupedStatement()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    database.Commands.Reset();

                    await database.NewAnalyticsService(db).GetMostBorrowedBooksAsync(DateRange.All, 5, default);

                    database.Commands.Count.ShouldBe(1);
                    database.Commands.Texts.ShouldHaveSingleItem().ShouldContain("GROUP BY");
                }
            }
        }

        [Fact]
        public async Task TopBorrowers_IsOneGroupedStatement()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    database.Commands.Reset();

                    await database.NewAnalyticsService(db).GetTopBorrowersAsync(FirstQuarter, 5, default);

                    database.Commands.Count.ShouldBe(1);
                    database.Commands.Texts.ShouldHaveSingleItem().ShouldContain("GROUP BY");
                }
            }
        }

        [Fact]
        public async Task ReadingPace_IsTwoStatements()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    database.Commands.Reset();

                    await database.NewAnalyticsService(db).GetReadingPaceAsync(3, DateRange.All, default);

                    // Existence check, then a narrow projection.
                    database.Commands.Count.ShouldBe(2);
                }
            }
        }

        [Fact]
        public async Task AlsoBorrowed_IsTwoStatementsAndKeepsTheCohortInSql()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    database.Commands.Reset();

                    await database.NewAnalyticsService(db).GetAlsoBorrowedBooksAsync(2, DateRange.All, 3, default);

                    // Existence + cohort size, then the ranking. A third means a materialised cohort.
                    database.Commands.Count.ShouldBe(2);
                    var ranking = database.Commands.Texts.Last();
                    ranking.ShouldContain("GROUP BY");
                    ranking.ShouldContain("IN (");
                }
            }
        }

        [Fact]
        public async Task Catalogue_IsOneStatement()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    database.Commands.Reset();

                    await database.NewLendingService(db).ListBooksAsync(default);

                    // Ten titles, sixteen copies; an N+1 would be 11 or 27 commands.
                    database.Commands.Count.ShouldBe(1);
                }
            }
        }
    }
}
