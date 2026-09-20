using Google.Protobuf.WellKnownTypes;
using Library.Domain.Analytics;
using Library.Service.Application.Models;
using Library.Service.Grpc;
using Proto = Library.Contracts.V1;

namespace Library.UnitTests.Service
{
    public class ProtoMappingTests
    {
        private static readonly DateTimeOffset From = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset To = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

        [Fact]
        public void ToDateRange_UnsetPeriod_IsAllTime()
        {
            var range = ProtoMapping.ToDateRange(null);

            range.ShouldBe(DateRange.All);
            range.IsBounded.ShouldBeFalse();
        }

        [Fact]
        public void ToDateRange_BothBounds_RoundTrips()
        {
            var range = ProtoMapping.ToDateRange(new Proto.DateRange
            {
                From = Timestamp.FromDateTimeOffset(From),
                To = Timestamp.FromDateTimeOffset(To),
            });

            range.From.ShouldBe(From);
            range.To.ShouldBe(To);
        }

        [Fact]
        public void ToDateRange_OneBound_LeavesTheOtherUnbounded()
        {
            var range = ProtoMapping.ToDateRange(new Proto.DateRange { From = Timestamp.FromDateTimeOffset(From) });

            range.From.ShouldBe(From);
            range.To.ShouldBeNull();
        }

        [Fact]
        public void ToProto_OpenLoan_LeavesReturnedAtUnset()
        {
            var loan = ProtoMapping.ToProto(new LoanView(1, 2, "Dune", 3, "Clara Diaz", From, To, null));

            loan.ReturnedAt.ShouldBeNull();
        }

        [Fact]
        public void ToProto_ClosedLoan_CarriesReturnedAt()
        {
            var loan = ProtoMapping.ToProto(new LoanView(1, 2, "Dune", 3, "Clara Diaz", From, To, To));

            loan.ReturnedAt.ShouldNotBeNull();
            loan.ReturnedAt.ToDateTimeOffset().ShouldBe(To);
        }

        [Fact]
        public void ToProto_NothingToEstimateFrom_LeavesPagesPerDayUnset()
        {
            var response = ProtoMapping.ToProto(new ReadingPaceView(1, "New Member", ReadingPaceEstimate.None));

            // Absence is not zero.
            response.HasPagesPerDay.ShouldBeFalse();
            response.CompletedLoans.ShouldBe(0);
        }

        [Fact]
        public void ToProto_Estimate_CarriesTheNumeratorDenominatorAndBreakdown()
        {
            var estimate = new ReadingPaceEstimate(
                115.0, 3, 1150, 10, new[] { new LoanPace(1, "The Hobbit", 300, 3, 100.0) });

            var response = ProtoMapping.ToProto(new ReadingPaceView(3, "Clara Diaz", estimate));

            response.HasPagesPerDay.ShouldBeTrue();
            response.PagesPerDay.ShouldBe(115.0);
            response.TotalPages.ShouldBe(1150);
            response.TotalDays.ShouldBe(10);
            response.Loans.ShouldHaveSingleItem().Title.ShouldBe("The Hobbit");
        }

        [Fact]
        public void ToProto_AlsoBorrowed_CarriesCohortSize()
        {
            var view = new AlsoBorrowedView(
                2, "Dune", 5, new[] { new AlsoBorrowedBookView(1, "The Hobbit", "Tolkien", 5, 5) });

            var response = ProtoMapping.ToProto(view);

            response.CohortSize.ShouldBe(5);
            response.Books.ShouldHaveSingleItem().SharedBorrowers.ShouldBe(5);
        }
    }
}
