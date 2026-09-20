using Grpc.Core;
using Library.Contracts.V1;
using Library.Service.Application;

namespace Library.Service.Grpc
{
    public sealed class AnalyticsGrpcService : Contracts.V1.AnalyticsService.AnalyticsServiceBase
    {
        private readonly IAnalyticsService _analytics;

        public AnalyticsGrpcService(IAnalyticsService analytics)
        {
            _analytics = analytics;
        }

        public override async Task<MostBorrowedBooksResponse> GetMostBorrowedBooks(
            MostBorrowedBooksRequest request, ServerCallContext context)
        {
            var books = await _analytics.GetMostBorrowedBooksAsync(
                ProtoMapping.ToDateRange(request.Period), request.Limit, context.CancellationToken);

            var response = new MostBorrowedBooksResponse();
            response.Books.AddRange(books.Select(ProtoMapping.ToProto));
            return response;
        }

        public override async Task<TopBorrowersResponse> GetTopBorrowers(
            TopBorrowersRequest request, ServerCallContext context)
        {
            var borrowers = await _analytics.GetTopBorrowersAsync(
                ProtoMapping.ToDateRange(request.Period), request.Limit, context.CancellationToken);

            var response = new TopBorrowersResponse();
            response.Borrowers.AddRange(borrowers.Select(ProtoMapping.ToProto));
            return response;
        }

        public override async Task<ReadingPaceResponse> GetReadingPace(
            ReadingPaceRequest request, ServerCallContext context)
        {
            return ProtoMapping.ToProto(await _analytics.GetReadingPaceAsync(
                request.BorrowerId, ProtoMapping.ToDateRange(request.Period), context.CancellationToken));
        }

        public override async Task<AlsoBorrowedResponse> GetAlsoBorrowedBooks(
            AlsoBorrowedRequest request, ServerCallContext context)
        {
            return ProtoMapping.ToProto(await _analytics.GetAlsoBorrowedBooksAsync(
                request.BookId, ProtoMapping.ToDateRange(request.Period), request.Limit, context.CancellationToken));
        }
    }
}
