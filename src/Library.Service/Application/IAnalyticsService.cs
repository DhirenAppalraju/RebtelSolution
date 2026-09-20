using Library.Domain.Analytics;
using Library.Service.Application.Models;

namespace Library.Service.Application
{
    public interface IAnalyticsService
    {
        Task<IReadOnlyList<MostBorrowedBookView>> GetMostBorrowedBooksAsync(DateRange period, int limit, CancellationToken ct);

        Task<IReadOnlyList<TopBorrowerView>> GetTopBorrowersAsync(DateRange period, int limit, CancellationToken ct);

        Task<ReadingPaceView> GetReadingPaceAsync(int borrowerId, DateRange period, CancellationToken ct);

        Task<AlsoBorrowedView> GetAlsoBorrowedBooksAsync(int bookId, DateRange period, int limit, CancellationToken ct);
    }
}
