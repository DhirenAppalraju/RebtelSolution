using Library.Service.Application.Models;

namespace Library.Service.Application
{
    public interface ILendingService
    {
        Task<BookView> AddBookAsync(string title, string author, int pageCount, int copies, CancellationToken ct);

        Task<BookView> GetBookAsync(int bookId, CancellationToken ct);

        Task<IReadOnlyList<BookView>> ListBooksAsync(CancellationToken ct);

        Task<BorrowerView> AddBorrowerAsync(string fullName, string email, CancellationToken ct);

        Task<BorrowerView> GetBorrowerAsync(int borrowerId, CancellationToken ct);

        Task<LoanView> BorrowBookAsync(int borrowerId, int bookId, CancellationToken ct);

        Task<LoanView> ReturnBookAsync(int loanId, CancellationToken ct);

        Task<LoanView> GetLoanAsync(int loanId, CancellationToken ct);
    }
}
