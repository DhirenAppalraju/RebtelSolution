using Library.Domain.Entities;
using Library.Domain.Exceptions;

namespace Library.Domain.Lending
{
    /// <summary>The borrow decision spans several rows, so it lives here rather than on an entity: the service gathers the facts, this decides.</summary>
    public static class LendingRules
    {
        public static Loan Borrow(
            int borrowerId,
            int bookId,
            int openLoanCount,
            bool alreadyHoldsTitle,
            int? freeCopyId,
            LendingPolicy policy,
            DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(policy);

            if (openLoanCount >= policy.MaxConcurrentLoans)
            {
                throw new ConflictException(
                    $"Borrower {borrowerId} already has {openLoanCount} open loans; the limit is {policy.MaxConcurrentLoans}.");
            }

            if (alreadyHoldsTitle)
            {
                throw new ConflictException($"Borrower {borrowerId} already holds a copy of book {bookId}.");
            }

            if (!freeCopyId.HasValue)
            {
                throw new ConflictException($"Every copy of book {bookId} is on loan.");
            }

            return Loan.Open(freeCopyId.Value, bookId, borrowerId, now, now.AddDays(policy.LoanPeriodDays));
        }
    }
}
