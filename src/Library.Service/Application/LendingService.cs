using Library.Domain.Entities;
using Library.Domain.Exceptions;
using Library.Domain.Lending;
using Library.Service.Application.Models;
using Library.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Library.Service.Application
{
    public sealed class LendingService : ILendingService
    {
        private readonly LibraryDbContext _db;
        private readonly LendingPolicy _policy;
        private readonly TimeProvider _clock;

        public LendingService(
            LibraryDbContext db,
            IOptions<LendingPolicy> policy,
            TimeProvider clock)
        {
            _db = db;
            _policy = policy.Value;
            _clock = clock;
        }

        public async Task<BookView> AddBookAsync(string title, string author, int pageCount, int copies, CancellationToken ct)
        {
            var book = Book.Create(title, author, pageCount, copies);
            _db.Books.Add(book);
            await SaveAsync(ct);

            return new BookView(book.Id, book.Title, book.Author, book.PageCount, copies, copies);
        }

        public async Task<BookView> GetBookAsync(int bookId, CancellationToken ct)
        {
            return await BookViews(_db.Books.AsNoTracking().Where(b => b.Id == bookId)).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException(nameof(Book), bookId);
        }

        public async Task<IReadOnlyList<BookView>> ListBooksAsync(CancellationToken ct)
        {
            return await BookViews(_db.Books.AsNoTracking().OrderBy(b => b.Id)).ToListAsync(ct);
        }

        public async Task<BorrowerView> AddBorrowerAsync(string fullName, string email, CancellationToken ct)
        {
            var borrower = Borrower.Create(fullName, email);
            _db.Borrowers.Add(borrower);
            await SaveAsync(ct);

            return new BorrowerView(borrower.Id, borrower.FullName, borrower.Email);
        }

        public async Task<BorrowerView> GetBorrowerAsync(int borrowerId, CancellationToken ct)
        {
            return await _db.Borrowers.AsNoTracking()
                .Where(b => b.Id == borrowerId)
                .Select(b => new BorrowerView(b.Id, b.FullName, b.Email))
                .SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException(nameof(Borrower), borrowerId);
        }

        public async Task<LoanView> BorrowBookAsync(int borrowerId, int bookId, CancellationToken ct)
        {
            var borrowerName = await _db.Borrowers.AsNoTracking()
                .Where(b => b.Id == borrowerId).Select(b => b.FullName).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException(nameof(Borrower), borrowerId);

            var bookTitle = await _db.Books.AsNoTracking()
                .Where(b => b.Id == bookId).Select(b => b.Title).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException(nameof(Book), bookId);

            // Both multi-row facts in one pass over the borrower's open loans.
            var held = await _db.Loans.AsNoTracking()
                .Where(l => l.BorrowerId == borrowerId && l.ReturnedAt == null)
                .Select(l => l.BookId)
                .ToListAsync(ct);

            var freeCopyId = await _db.BookCopies.AsNoTracking()
                .Where(c => c.BookId == bookId && !_db.Loans.Any(l => l.BookCopyId == c.Id && l.ReturnedAt == null))
                .OrderBy(c => c.Id)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct);

            //
            // The concurrent-loan limit is the one rule with no database backstop. Two simultaneous
            // borrows of *different* titles both read the same `held.Count` and both insert: neither
            // filtered unique index covers that pair, so a member can finish one over the limit.
            // Accepted, bounded and benign - closing it costs a serialisable transaction on every
            // borrow. The last-copy and one-title-per-borrower races ARE closed, by the indexes.
            var loan = LendingRules.Borrow(
                borrowerId,
                bookId,
                held.Count,
                held.Contains(bookId),
                freeCopyId,
                _policy,
                _clock.GetUtcNow());

            _db.Loans.Add(loan);
            await SaveAsync(ct);

            return new LoanView(loan.Id, bookId, bookTitle, borrowerId, borrowerName, loan.BorrowedAt, loan.DueAt, loan.ReturnedAt);
        }

        public async Task<LoanView> ReturnBookAsync(int loanId, CancellationToken ct)
        {
            var loan = await _db.Loans
                .Include(l => l.Book)
                .Include(l => l.Borrower)
                .SingleOrDefaultAsync(l => l.Id == loanId, ct)
                ?? throw new NotFoundException(nameof(Loan), loanId);

            loan.Return(_clock.GetUtcNow());

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // The concurrency token matched zero rows: someone else returned it first.
                throw new ConflictException($"Loan {loanId} was already returned.");
            }

            return ToView(loan);
        }

        public async Task<LoanView> GetLoanAsync(int loanId, CancellationToken ct)
        {
            return await _db.Loans.AsNoTracking()
                .Where(l => l.Id == loanId)
                .Select(l => new LoanView(
                    l.Id, l.BookId, l.Book!.Title, l.BorrowerId, l.Borrower!.FullName,
                    l.BorrowedAt, l.DueAt, l.ReturnedAt))
                .SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException(nameof(Loan), loanId);
        }

        // Filter and order before projecting: EF cannot translate a predicate or an ordering
        // that reads a property off a constructor-projected type.
        private IQueryable<BookView> BookViews(IQueryable<Book> books)
        {
            return books.Select(b => new BookView(
                b.Id,
                b.Title,
                b.Author,
                b.PageCount,
                b.Copies.Count(),
                b.Copies.Count(c => !_db.Loans.Any(l => l.BookCopyId == c.Id && l.ReturnedAt == null))));
        }

        private static LoanView ToView(Loan loan)
        {
            return new LoanView(loan.Id, loan.BookId, loan.Book!.Title, loan.BorrowerId, loan.Borrower!.FullName,
                loan.BorrowedAt, loan.DueAt, loan.ReturnedAt);
        }

        private async Task SaveAsync(CancellationToken ct)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException error)
            {
                var conflict = DatabaseErrors.AsConflict(error);
                if (conflict == null)
                {
                    throw;
                }

                throw conflict;
            }
        }
    }
}
