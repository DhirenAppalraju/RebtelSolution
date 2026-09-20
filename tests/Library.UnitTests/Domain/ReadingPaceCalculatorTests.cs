using Library.Domain.Analytics;

namespace Library.UnitTests.Domain
{
    public class ReadingPaceCalculatorTests
    {
        private static DateTimeOffset Jan(int day, int hour = 10)
        {
            return new DateTimeOffset(2026, 1, day, hour, 0, 0, TimeSpan.Zero);
        }

        private static CompletedLoan Loan(int bookId, string title, int pages, DateTimeOffset from, DateTimeOffset to)
        {
            return new CompletedLoan(bookId, title, pages, from, to);
        }

        [Fact]
        public void Estimate_NoLoans_HasNothingToEstimateFrom()
        {
            var estimate = ReadingPaceCalculator.Estimate(new CompletedLoan[0]);

            estimate.PagesPerDay.ShouldBeNull();
            estimate.CompletedLoans.ShouldBe(0);
            estimate.TotalPages.ShouldBe(0);
            estimate.TotalDays.ShouldBe(0);
            estimate.Loans.ShouldBeEmpty();
        }

        [Fact]
        public void Estimate_SingleLoan_IsPagesOverDays()
        {
            var estimate = ReadingPaceCalculator.Estimate(
                new[] { Loan(1, "The Hobbit", 300, Jan(2), Jan(5)) });

            estimate.PagesPerDay.ShouldBe(100.0);
            estimate.TotalPages.ShouldBe(300);
            estimate.TotalDays.ShouldBe(3);
        }

        [Fact]
        public void Estimate_ClarasThreeLoans_Is115PagesPerDay()
        {
            var estimate = ReadingPaceCalculator.Estimate(new[]
            {
                Loan(1, "The Hobbit", 300, Jan(5), Jan(8)),
                Loan(2, "Dune", 600, Jan(10), Jan(16)),
                Loan(3, "Neuromancer", 250, new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 1, 16, 0, 0, TimeSpan.Zero)),
            });

            estimate.PagesPerDay.ShouldBe(115.0);
            estimate.CompletedLoans.ShouldBe(3);
            estimate.TotalPages.ShouldBe(1150);
            estimate.TotalDays.ShouldBe(10);
            estimate.Loans.Select(l => l.PagesPerDay).ShouldBe(new double[] { 100.0, 100.0, 250.0 });
        }

        [Fact]
        public void Estimate_OverlappingLoans_AreNotNettedOut()
        {
            // Ava's January: 820 pages over 4 + 6 days. Merging the intervals would give 102.5.
            var estimate = ReadingPaceCalculator.Estimate(new[]
            {
                Loan(5, "Clean Code", 400, Jan(2), Jan(6)),
                Loan(6, "Refactoring", 420, Jan(4), Jan(10)),
            });

            estimate.PagesPerDay.ShouldBe(82.0);
            estimate.TotalDays.ShouldBe(10);
        }

        [Fact]
        public void Estimate_LongBook_DominatesTheAverageProportionally()
        {
            var estimate = ReadingPaceCalculator.Estimate(new[]
            {
                Loan(1, "Pamphlet", 30, Jan(2), Jan(3)),
                Loan(2, "Doorstop", 900, Jan(4), Jan(13)),
            });

            // 930 / 10, not the mean of 30 and 100.
            estimate.PagesPerDay.ShouldBe(93.0);
        }

        [Fact]
        public void Estimate_LoansSuppliedOutOfOrder_AreReportedByBorrowDate()
        {
            var estimate = ReadingPaceCalculator.Estimate(new[]
            {
                Loan(2, "Second", 600, Jan(10), Jan(16)),
                Loan(1, "First", 300, Jan(5), Jan(8)),
            });

            estimate.Loans.Select(l => l.Title).ShouldBe(new[] { "First", "Second" });
            estimate.TotalPages.ShouldBe(900);
            estimate.TotalDays.ShouldBe(9);
        }

        [Fact]
        public void WholeDays_SixHours_CountsAsOneDay()
        {
            ReadingPaceCalculator.WholeDays(Jan(1), Jan(1, 16)).ShouldBe(1);
        }

        [Fact]
        public void WholeDays_ExactlyThreeDays_IsNotRoundedUpFurther()
        {
            ReadingPaceCalculator.WholeDays(Jan(1), Jan(4)).ShouldBe(3);
        }

        [Fact]
        public void WholeDays_ThreeDaysAndOneMinute_IsFour()
        {
            ReadingPaceCalculator.WholeDays(Jan(1), Jan(4).AddMinutes(1)).ShouldBe(4);
        }

        [Fact]
        public void WholeDays_ReturnBeforeBorrow_Throws()
        {
            Should.Throw<ArgumentOutOfRangeException>(() => ReadingPaceCalculator.WholeDays(Jan(5), Jan(4)));
        }
    }
}
