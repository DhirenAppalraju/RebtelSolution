using Library.Domain.Exceptions;
using Library.Domain.Lending;

namespace Library.UnitTests.Domain
{
    public class LendingRulesTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);
        private static readonly LendingPolicy Policy = new LendingPolicy { LoanPeriodDays = 21, MaxConcurrentLoans = 5 };

        [Fact]
        public void Borrow_FreeCopyUnderTheLimit_OpensTheLoan()
        {
            var loan = LendingRules.Borrow(1, 2, openLoanCount: 4, alreadyHoldsTitle: false, freeCopyId: 7, Policy, Now);

            loan.BorrowerId.ShouldBe(1);
            loan.BookId.ShouldBe(2);
            loan.BookCopyId.ShouldBe(7);
            loan.BorrowedAt.ShouldBe(Now);
            loan.DueAt.ShouldBe(Now.AddDays(21));
            loan.ReturnedAt.ShouldBeNull();
            loan.IsOpen.ShouldBeTrue();
        }

        [Fact]
        public void Borrow_AtTheConcurrentLimit_ThrowsNamingTheLimit()
        {
            var error = Should.Throw<ConflictException>(() =>
                LendingRules.Borrow(1, 2, openLoanCount: 5, alreadyHoldsTitle: false, freeCopyId: 7, Policy, Now));

            error.Message.ShouldContain("the limit is 5");
        }

        [Fact]
        public void Borrow_AlreadyHoldsTheTitle_Throws()
        {
            Should.Throw<ConflictException>(() =>
                LendingRules.Borrow(1, 2, openLoanCount: 0, alreadyHoldsTitle: true, freeCopyId: 7, Policy, Now))
                .Message.ShouldContain("already holds a copy");
        }

        [Fact]
        public void Borrow_NoFreeCopy_Throws()
        {
            Should.Throw<ConflictException>(() =>
                LendingRules.Borrow(1, 2, openLoanCount: 0, alreadyHoldsTitle: false, freeCopyId: null, Policy, Now))
                .Message.ShouldContain("Every copy");
        }

        [Fact]
        public void Borrow_AtTheLimitAndHoldingTheTitle_ReportsTheLimitFirst()
        {
            // Order matters: report the limit, the actionable rule.
            var error = Should.Throw<ConflictException>(() =>
                LendingRules.Borrow(1, 2, openLoanCount: 5, alreadyHoldsTitle: true, freeCopyId: null, Policy, Now));

            error.Message.ShouldContain("open loans");
        }
    }
}
