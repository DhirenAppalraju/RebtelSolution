using Library.Domain.Analytics;
using Library.Domain.Entities;

namespace Library.Service.Application
{
    public static class QueryExtensions
    {
        /// <summary>Every report windows on BorrowedAt, half-open: >= from and < to.</summary>
        public static IQueryable<Loan> BorrowedWithin(this IQueryable<Loan> loans, DateRange period)
        {
            if (period.From.HasValue)
            {
                var from = period.From.Value;
                loans = loans.Where(l => l.BorrowedAt >= from);
            }

            if (period.To.HasValue)
            {
                var to = period.To.Value;
                loans = loans.Where(l => l.BorrowedAt < to);
            }

            return loans;
        }
    }
}
