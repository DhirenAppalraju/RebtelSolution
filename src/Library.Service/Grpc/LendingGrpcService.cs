using Grpc.Core;
using Library.Contracts.V1;
using Library.Service.Application;

namespace Library.Service.Grpc
{
    /// <summary>Thin facade: unwrap the request, call the application service, map the result. Rules live deeper.</summary>
    public sealed class LendingGrpcService : Contracts.V1.LendingService.LendingServiceBase
    {
        private readonly ILendingService _lending;

        public LendingGrpcService(ILendingService lending)
        {
            _lending = lending;
        }

        public override async Task<Book> AddBook(AddBookRequest request, ServerCallContext context)
        {
            return ProtoMapping.ToProto(await _lending.AddBookAsync(
                request.Title, request.Author, request.PageCount, request.Copies, context.CancellationToken));
        }

        public override async Task<Book> GetBook(GetBookRequest request, ServerCallContext context)
        {
            return ProtoMapping.ToProto(await _lending.GetBookAsync(request.BookId, context.CancellationToken));
        }

        public override async Task<ListBooksResponse> ListBooks(ListBooksRequest request, ServerCallContext context)
        {
            var books = await _lending.ListBooksAsync(context.CancellationToken);

            var response = new ListBooksResponse();
            response.Books.AddRange(books.Select(ProtoMapping.ToProto));
            return response;
        }

        public override async Task<Borrower> AddBorrower(AddBorrowerRequest request, ServerCallContext context)
        {
            return ProtoMapping.ToProto(await _lending.AddBorrowerAsync(request.FullName, request.Email, context.CancellationToken));
        }

        public override async Task<Borrower> GetBorrower(GetBorrowerRequest request, ServerCallContext context)
        {
            return ProtoMapping.ToProto(await _lending.GetBorrowerAsync(request.BorrowerId, context.CancellationToken));
        }

        public override async Task<Loan> BorrowBook(BorrowBookRequest request, ServerCallContext context)
        {
            return ProtoMapping.ToProto(await _lending.BorrowBookAsync(request.BorrowerId, request.BookId, context.CancellationToken));
        }

        public override async Task<Loan> ReturnBook(ReturnBookRequest request, ServerCallContext context)
        {
            return ProtoMapping.ToProto(await _lending.ReturnBookAsync(request.LoanId, context.CancellationToken));
        }

        public override async Task<Loan> GetLoan(GetLoanRequest request, ServerCallContext context)
        {
            return ProtoMapping.ToProto(await _lending.GetLoanAsync(request.LoanId, context.CancellationToken));
        }
    }
}
