using Library.Api.Contracts;
using Proto = Library.Contracts.V1;

namespace Library.Api
{
    /// <summary>Protobuf in, public JSON out. Hand-written: a compile error beats a silently-null property.</summary>
    public static class ApiMapping
    {
        public static BookResponse ToResponse(Proto.Book book)
        {
            return new BookResponse(
                book.Id, book.Title, book.Author, book.PageCount, book.TotalCopies, book.AvailableCopies);
        }

        public static BorrowerResponse ToResponse(Proto.Borrower borrower)
        {
            return new BorrowerResponse(borrower.Id, borrower.FullName, borrower.Email);
        }

        public static LoanResponse ToResponse(Proto.Loan loan)
        {
            // Unset means still out; the JSON contract says that with a real null.
            DateTimeOffset? returnedAt = null;
            if (loan.ReturnedAt != null)
            {
                returnedAt = loan.ReturnedAt.ToDateTimeOffset();
            }

            return new LoanResponse(
                loan.Id,
                loan.BookId,
                loan.BookTitle,
                loan.BorrowerId,
                loan.BorrowerName,
                loan.BorrowedAt.ToDateTimeOffset(),
                loan.DueAt.ToDateTimeOffset(),
                returnedAt);
        }

        public static BookListResponse ToResponse(Proto.ListBooksResponse response)
        {
            var books = new List<BookResponse>();
            foreach (var book in response.Books)
            {
                books.Add(ToResponse(book));
            }

            return new BookListResponse(books);
        }

        public static MostBorrowedBooksResponse ToResponse(Proto.MostBorrowedBooksResponse response)
        {
            var books = new List<MostBorrowedBookResponse>();
            foreach (var book in response.Books)
            {
                books.Add(new MostBorrowedBookResponse(
                    book.BookId, book.Title, book.Author, book.BorrowCount, book.DistinctBorrowers));
            }

            return new MostBorrowedBooksResponse(books);
        }

        public static TopBorrowersResponse ToResponse(Proto.TopBorrowersResponse response)
        {
            var borrowers = new List<TopBorrowerResponse>();
            foreach (var borrower in response.Borrowers)
            {
                borrowers.Add(new TopBorrowerResponse(
                    borrower.BorrowerId, borrower.FullName, borrower.LoanCount, borrower.DistinctTitles));
            }

            return new TopBorrowersResponse(borrowers);
        }

        public static ReadingPaceResponse ToResponse(Proto.ReadingPaceResponse response)
        {
            var loans = new List<LoanPaceResponse>();
            foreach (var loan in response.Loans)
            {
                loans.Add(new LoanPaceResponse(
                    loan.BookId, loan.Title, loan.Pages, loan.Days, Round(loan.PagesPerDay)));
            }

            double? pagesPerDay = null;
            if (response.HasPagesPerDay)
            {
                pagesPerDay = Round(response.PagesPerDay);
            }

            return new ReadingPaceResponse(
                response.BorrowerId,
                response.FullName,
                pagesPerDay,
                response.CompletedLoans,
                response.TotalPages,
                response.TotalDays,
                loans);
        }

        public static AlsoBorrowedResponse ToResponse(Proto.AlsoBorrowedResponse response)
        {
            var books = new List<AlsoBorrowedBookResponse>();
            foreach (var book in response.Books)
            {
                books.Add(new AlsoBorrowedBookResponse(
                    book.BookId, book.Title, book.Author, book.SharedBorrowers, book.LoanCount));
            }

            return new AlsoBorrowedResponse(response.BookId, response.Title, response.CohortSize, books);
        }

        // Rounding happens at the transport edge, never in the domain.
        private static double Round(double value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}
