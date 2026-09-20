using Google.Protobuf.WellKnownTypes;
using Library.Contracts.V1;
using Library.Service.Application.Models;
using DomainDateRange = Library.Domain.Analytics.DateRange;
using ProtoDateRange = Library.Contracts.V1.DateRange;

namespace Library.Service.Grpc
{
    /// <summary>Hand-written both ways: generated messages carry machinery that has no place in the application layer.</summary>
    public static class ProtoMapping
    {
        public static DomainDateRange ToDateRange(ProtoDateRange? period)
        {
            return period == null
                ? DomainDateRange.All
                : new DomainDateRange(ToInstant(period.From), ToInstant(period.To));
        }

        public static Contracts.V1.Book ToProto(BookView view)
        {
            return new Contracts.V1.Book
            {
                Id = view.Id,
                Title = view.Title,
                Author = view.Author,
                PageCount = view.PageCount,
                TotalCopies = view.TotalCopies,
                AvailableCopies = view.AvailableCopies,
            };
        }

        public static Contracts.V1.Borrower ToProto(BorrowerView view)
        {
            return new Contracts.V1.Borrower
            {
                Id = view.Id,
                FullName = view.FullName,
                Email = view.Email,
            };
        }

        public static Contracts.V1.Loan ToProto(LoanView view)
        {
            var loan = new Contracts.V1.Loan
            {
                Id = view.Id,
                BookId = view.BookId,
                BookTitle = view.BookTitle,
                BorrowerId = view.BorrowerId,
                BorrowerName = view.BorrowerName,
                BorrowedAt = Timestamp.FromDateTimeOffset(view.BorrowedAt),
                DueAt = Timestamp.FromDateTimeOffset(view.DueAt),
            };

            // Left unset when still out; a message field's absence is the null.
            if (view.ReturnedAt.HasValue)
            {
                loan.ReturnedAt = Timestamp.FromDateTimeOffset(view.ReturnedAt.Value);
            }

            return loan;
        }

        public static MostBorrowedBook ToProto(MostBorrowedBookView view)
        {
            return new MostBorrowedBook
            {
                BookId = view.BookId,
                Title = view.Title,
                Author = view.Author,
                BorrowCount = view.BorrowCount,
                DistinctBorrowers = view.DistinctBorrowers,
            };
        }

        public static TopBorrower ToProto(TopBorrowerView view)
        {
            return new TopBorrower
            {
                BorrowerId = view.BorrowerId,
                FullName = view.FullName,
                LoanCount = view.LoanCount,
                DistinctTitles = view.DistinctTitles,
            };
        }

        public static ReadingPaceResponse ToProto(ReadingPaceView view)
        {
            var estimate = view.Estimate;
            var response = new ReadingPaceResponse
            {
                BorrowerId = view.BorrowerId,
                FullName = view.FullName,
                CompletedLoans = estimate.CompletedLoans,
                TotalPages = estimate.TotalPages,
                TotalDays = estimate.TotalDays,
            };

            // Absence is not zero: unset when there is nothing to estimate from.
            if (estimate.PagesPerDay.HasValue)
            {
                response.PagesPerDay = estimate.PagesPerDay.Value;
            }

            response.Loans.AddRange(estimate.Loans.Select(l => new Contracts.V1.LoanPace
            {
                BookId = l.BookId,
                Title = l.Title,
                Pages = l.Pages,
                Days = l.Days,
                PagesPerDay = l.PagesPerDay,
            }));

            return response;
        }

        public static AlsoBorrowedResponse ToProto(AlsoBorrowedView view)
        {
            var response = new AlsoBorrowedResponse
            {
                BookId = view.BookId,
                Title = view.Title,
                CohortSize = view.CohortSize,
            };

            response.Books.AddRange(view.Books.Select(b => new Contracts.V1.AlsoBorrowedBook
            {
                BookId = b.BookId,
                Title = b.Title,
                Author = b.Author,
                SharedBorrowers = b.SharedBorrowers,
                LoanCount = b.LoanCount,
            }));

            return response;
        }

        private static DateTimeOffset? ToInstant(Timestamp? timestamp)
        {
            return timestamp?.ToDateTimeOffset();
        }
    }
}
