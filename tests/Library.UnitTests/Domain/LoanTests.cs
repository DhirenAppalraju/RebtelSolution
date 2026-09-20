using Library.Domain.Entities;
using Library.Domain.Exceptions;

namespace Library.UnitTests.Domain
{
    public class LoanTests
    {
        private static readonly DateTimeOffset Borrowed = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        private static Loan OpenLoan()
        {
            return Loan.Open(1, 2, 3, Borrowed, Borrowed.AddDays(21));
        }

        [Fact]
        public void Return_Twice_Throws()
        {
            var loan = OpenLoan();
            loan.Return(Borrowed.AddDays(3));

            loan.IsOpen.ShouldBeFalse();
            Should.Throw<ConflictException>(() => loan.Return(Borrowed.AddDays(4)))
                .Message.ShouldContain("already returned");
        }

        [Fact]
        public void Return_BeforeTheBorrow_Throws()
        {
            Should.Throw<ConflictException>(() => OpenLoan().Return(Borrowed.AddDays(-1)));
        }

        [Fact]
        public void Return_RecordsTheInstant()
        {
            var loan = OpenLoan();
            var now = Borrowed.AddDays(3);

            loan.Return(now);

            loan.ReturnedAt.ShouldBe(now);
        }
    }
}
