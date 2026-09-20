using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Proto = Library.Contracts.V1;

namespace Library.FunctionalTests.Service
{
    public class AnalyticsFeatureTests : IClassFixture<ServiceHostFixture>
    {
        private readonly ServiceHostFixture _fixture;

        public AnalyticsFeatureTests(ServiceHostFixture fixture)
        {
            _fixture = fixture;
        }

        private static Proto.DateRange Period(DateTimeOffset from, DateTimeOffset to)
        {
            return new Proto.DateRange
            {
                From = Timestamp.FromDateTimeOffset(from),
                To = Timestamp.FromDateTimeOffset(to),
            };
        }

        private static DateTimeOffset Utc(int month, int day)
        {
            return new DateTimeOffset(2026, month, day, 0, 0, 0, TimeSpan.Zero);
        }

        [Fact]
        public async Task GetMostBorrowedBooks_ReturnsTheRankingTheReadmePublishes()
        {
            var response = await _fixture.Analytics.GetMostBorrowedBooksAsync(
                new Proto.MostBorrowedBooksRequest { Limit = 5 });

            response.Books.Select(b => (b.Title, b.BorrowCount, b.DistinctBorrowers)).ShouldBe(new[]
            {
                ("The Hobbit", 6, 6),
                ("Dune", 6, 5),
                ("Neuromancer", 3, 3),
                ("Sapiens", 3, 3),
                ("Clean Code", 3, 2),
            });
        }

        [Fact]
        public async Task GetTopBorrowers_FirstQuarter_PutsAvaFirst()
        {
            var response = await _fixture.Analytics.GetTopBorrowersAsync(new Proto.TopBorrowersRequest
            {
                Period = Period(Utc(1, 1), Utc(4, 1)),
                Limit = 3,
            });

            var top = response.Borrowers[0];
            top.FullName.ShouldBe("Ava Chen");
            top.LoanCount.ShouldBe(6);
            top.DistinctTitles.ShouldBe(5);
        }

        [Fact]
        public async Task GetTopBorrowers_WithoutAPeriod_IsInvalidArgument()
        {
            var error = await Should.ThrowAsync<RpcException>(() => _fixture.Analytics
                .GetTopBorrowersAsync(new Proto.TopBorrowersRequest { Limit = 5 })
                .ResponseAsync);

            error.StatusCode.ShouldBe(StatusCode.InvalidArgument);
            error.Status.Detail.ShouldContain("required");
        }

        [Fact]
        public async Task GetReadingPace_Clara_Is115WithThreeLoansInTheBreakdown()
        {
            var response = await _fixture.Analytics.GetReadingPaceAsync(new Proto.ReadingPaceRequest { BorrowerId = 3 });

            response.FullName.ShouldBe("Clara Diaz");
            response.HasPagesPerDay.ShouldBeTrue();
            response.PagesPerDay.ShouldBe(115.0);
            response.TotalPages.ShouldBe(1150);
            response.TotalDays.ShouldBe(10);
            response.Loans.Count.ShouldBe(3);
        }

        [Fact]
        public async Task GetReadingPace_NewBorrower_LeavesPagesPerDayUnset()
        {
            var borrower = await _fixture.Lending.AddBorrowerAsync(new Proto.AddBorrowerRequest
            {
                FullName = "Unread Member",
                Email = $"unread-{Guid.NewGuid():N}@example.com",
            });

            var response = await _fixture.Analytics.GetReadingPaceAsync(
                new Proto.ReadingPaceRequest { BorrowerId = borrower.Id });

            response.HasPagesPerDay.ShouldBeFalse();
            response.CompletedLoans.ShouldBe(0);
        }

        [Fact]
        public async Task GetReadingPace_UnknownBorrower_IsNotFound()
        {
            var error = await Should.ThrowAsync<RpcException>(() => _fixture.Analytics
                .GetReadingPaceAsync(new Proto.ReadingPaceRequest { BorrowerId = 9999 })
                .ResponseAsync);

            error.StatusCode.ShouldBe(StatusCode.NotFound);
        }

        [Fact]
        public async Task GetAlsoBorrowedBooks_Dune_ReportsCohortFiveAndTheHobbitFirst()
        {
            var response = await _fixture.Analytics.GetAlsoBorrowedBooksAsync(
                new Proto.AlsoBorrowedRequest { BookId = 2, Limit = 3 });

            response.Title.ShouldBe("Dune");
            response.CohortSize.ShouldBe(5);
            response.Books[0].Title.ShouldBe("The Hobbit");
            response.Books[0].SharedBorrowers.ShouldBe(5);
            response.Books[0].LoanCount.ShouldBe(5);
        }

        [Fact]
        public async Task Reports_LimitAboveTheCap_IsInvalidArgumentNamingTheCap()
        {
            var error = await Should.ThrowAsync<RpcException>(() => _fixture.Analytics
                .GetMostBorrowedBooksAsync(new Proto.MostBorrowedBooksRequest { Limit = 101 })
                .ResponseAsync);

            error.StatusCode.ShouldBe(StatusCode.InvalidArgument);
            error.Status.Detail.ShouldContain("100");
        }
    }
}
