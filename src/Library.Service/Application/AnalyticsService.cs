using Library.Domain.Analytics;
using Library.Domain.Entities;
using Library.Domain.Exceptions;
using Library.Domain.Lending;
using Library.Service.Application.Models;
using Library.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Library.Service.Application
{
    /// <summary>
    /// Four aggregations over the Loans fact table. One GROUP BY each, at most `limit` rows;
    /// a command-count test pins the round trips.
    /// </summary>
    /// <remarks>
    /// Two EF translation rules: group on plain columns only, and rank before projecting
    /// into a view type. Mapping runs over at most `limit` rows.
    /// </remarks>
    public sealed class AnalyticsService : IAnalyticsService
    {
        private readonly LibraryDbContext _db;
        private readonly LendingPolicy _policy;

        public AnalyticsService(LibraryDbContext db, IOptions<LendingPolicy> policy)
        {
            _db = db;
            _policy = policy.Value;
        }

        public async Task<IReadOnlyList<MostBorrowedBookView>> GetMostBorrowedBooksAsync(
            DateRange period, int limit, CancellationToken ct)
        {
            var counts = _db.Loans.AsNoTracking().BorrowedWithin(period)
                .GroupBy(l => l.BookId)
                .Select(g => new
                {
                    BookId = g.Key,
                    BorrowCount = g.Count(),
                    DistinctBorrowers = g.Select(l => l.BorrowerId).Distinct().Count(),
                });

            var ranked = await counts
                .Join(_db.Books.AsNoTracking(), c => c.BookId, b => b.Id, (c, b) => new
                {
                    c.BookId,
                    b.Title,
                    b.Author,
                    c.BorrowCount,
                    c.DistinctBorrowers,
                })
                .OrderByDescending(x => x.BorrowCount)
                .ThenByDescending(x => x.DistinctBorrowers)
                .ThenBy(x => x.BookId)
                .Take(Resolve(limit))
                .ToListAsync(ct);

            var views = new List<MostBorrowedBookView>();
            foreach (var x in ranked)
            {
                views.Add(new MostBorrowedBookView(x.BookId, x.Title, x.Author, x.BorrowCount, x.DistinctBorrowers));
            }

            return views;
        }

        public async Task<IReadOnlyList<TopBorrowerView>> GetTopBorrowersAsync(
            DateRange period, int limit, CancellationToken ct)
        {
            if (!period.IsBounded)
            {
                throw new ValidationException(
                    "'from' and 'to' are required for top borrowers; the window is half-open, [from, to).");
            }

            var counts = _db.Loans.AsNoTracking().BorrowedWithin(period)
                .GroupBy(l => l.BorrowerId)
                .Select(g => new
                {
                    BorrowerId = g.Key,
                    LoanCount = g.Count(),
                    DistinctTitles = g.Select(l => l.BookId).Distinct().Count(),
                });

            var ranked = await counts
                .Join(_db.Borrowers.AsNoTracking(), c => c.BorrowerId, b => b.Id, (c, b) => new
                {
                    c.BorrowerId,
                    b.FullName,
                    c.LoanCount,
                    c.DistinctTitles,
                })
                .OrderByDescending(x => x.LoanCount)
                .ThenByDescending(x => x.DistinctTitles)
                .ThenBy(x => x.BorrowerId)
                .Take(Resolve(limit))
                .ToListAsync(ct);

            var views = new List<TopBorrowerView>();
            foreach (var x in ranked)
            {
                views.Add(new TopBorrowerView(x.BorrowerId, x.FullName, x.LoanCount, x.DistinctTitles));
            }

            return views;
        }

        public async Task<ReadingPaceView> GetReadingPaceAsync(int borrowerId, DateRange period, CancellationToken ct)
        {
            var fullName = await _db.Borrowers.AsNoTracking()
                .Where(b => b.Id == borrowerId).Select(b => b.FullName).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException(nameof(Borrower), borrowerId);

            // Narrow projection; arithmetic is a pure domain function.
            var completed = await _db.Loans.AsNoTracking().BorrowedWithin(period)
                .Where(l => l.BorrowerId == borrowerId && l.ReturnedAt != null)
                .Select(l => new { l.BookId, l.Book!.Title, l.Book!.PageCount, l.BorrowedAt, l.ReturnedAt })
                .ToListAsync(ct);

            var loans = completed
                .Select(l => new CompletedLoan(l.BookId, l.Title, l.PageCount, l.BorrowedAt, l.ReturnedAt!.Value))
                .ToList();

            return new ReadingPaceView(borrowerId, fullName, ReadingPaceCalculator.Estimate(loans));
        }

        /// <summary>
        /// Window applies to the ranked list, not the cohort. Cohort is all-time by design:
        /// windowing it collapses to too few readers to rank. The API description says so.
        /// </summary>
        public async Task<AlsoBorrowedView> GetAlsoBorrowedBooksAsync(
            int bookId, DateRange period, int limit, CancellationToken ct)
        {
            var source = await _db.Books.AsNoTracking()
                .Where(b => b.Id == bookId)
                .Select(b => new
                {
                    b.Title,
                    // Un-windowed; see the summary.
                    CohortSize = _db.Loans.Where(l => l.BookId == bookId).Select(l => l.BorrowerId).Distinct().Count(),
                })
                .SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException(nameof(Book), bookId);

            // Un-materialised: WHERE BorrowerId IN (SELECT ...). Materialising ships every id as a
            // parameter. Same membership CohortSize counts, so the two agree.
            var cohort = _db.Loans.Where(l => l.BookId == bookId).Select(l => l.BorrowerId).Distinct();

            var counts = _db.Loans.AsNoTracking().BorrowedWithin(period)
                .Where(l => cohort.Contains(l.BorrowerId) && l.BookId != bookId)
                .GroupBy(l => l.BookId)
                .Select(g => new
                {
                    BookId = g.Key,
                    SharedBorrowers = g.Select(l => l.BorrowerId).Distinct().Count(),
                    LoanCount = g.Count(),
                });

            var ranked = await counts
                .Join(_db.Books.AsNoTracking(), c => c.BookId, b => b.Id, (c, b) => new
                {
                    c.BookId,
                    b.Title,
                    b.Author,
                    c.SharedBorrowers,
                    c.LoanCount,
                })
                .OrderByDescending(x => x.SharedBorrowers)
                .ThenByDescending(x => x.LoanCount)
                .ThenBy(x => x.BookId)
                .Take(Resolve(limit))
                .ToListAsync(ct);

            var books = new List<AlsoBorrowedBookView>();
            foreach (var x in ranked)
            {
                books.Add(new AlsoBorrowedBookView(
                    x.BookId, x.Title, x.Author, x.SharedBorrowers, x.LoanCount));
            }

            return new AlsoBorrowedView(bookId, source.Title, source.CohortSize, books);
        }

        /// <summary>0 means default; over the cap is rejected, not clamped.</summary>
        private int Resolve(int limit)
        {
            if (limit == 0)
            {
                return _policy.DefaultReportLimit;
            }

            if (limit < 0)
            {
                throw new ValidationException("'limit' must not be negative.");
            }

            if (limit > _policy.MaxReportLimit)
            {
                throw new ValidationException($"'limit' must be between 1 and {_policy.MaxReportLimit}.");
            }

            return limit;
        }
    }
}
