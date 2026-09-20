using Library.Domain.Analytics;

namespace Library.Service.Application.Models
{

    public sealed class BookView
    {
        public BookView(
            int id,
            string title,
            string author,
            int pageCount,
            int totalCopies,
            int availableCopies)
        {
            Id = id;
            Title = title;
            Author = author;
            PageCount = pageCount;
            TotalCopies = totalCopies;
            AvailableCopies = availableCopies;
        }

        public int Id { get; }

        public string Title { get; }

        public string Author { get; }

        public int PageCount { get; }

        public int TotalCopies { get; }

        public int AvailableCopies { get; }
    }

    public sealed class BorrowerView
    {
        public BorrowerView(int id, string fullName, string email)
        {
            Id = id;
            FullName = fullName;
            Email = email;
        }

        public int Id { get; }

        public string FullName { get; }

        public string Email { get; }
    }

    public sealed class LoanView
    {
        public LoanView(
            int id,
            int bookId,
            string bookTitle,
            int borrowerId,
            string borrowerName,
            DateTimeOffset borrowedAt,
            DateTimeOffset dueAt,
            DateTimeOffset? returnedAt)
        {
            Id = id;
            BookId = bookId;
            BookTitle = bookTitle;
            BorrowerId = borrowerId;
            BorrowerName = borrowerName;
            BorrowedAt = borrowedAt;
            DueAt = dueAt;
            ReturnedAt = returnedAt;
        }

        public int Id { get; }

        public int BookId { get; }

        public string BookTitle { get; }

        public int BorrowerId { get; }

        public string BorrowerName { get; }

        public DateTimeOffset BorrowedAt { get; }

        public DateTimeOffset DueAt { get; }

        public DateTimeOffset? ReturnedAt { get; }
    }

    public sealed class MostBorrowedBookView
    {
        public MostBorrowedBookView(
            int bookId,
            string title,
            string author,
            int borrowCount,
            int distinctBorrowers)
        {
            BookId = bookId;
            Title = title;
            Author = author;
            BorrowCount = borrowCount;
            DistinctBorrowers = distinctBorrowers;
        }

        public int BookId { get; }

        public string Title { get; }

        public string Author { get; }

        public int BorrowCount { get; }

        public int DistinctBorrowers { get; }
    }

    public sealed class TopBorrowerView
    {
        public TopBorrowerView(
            int borrowerId,
            string fullName,
            int loanCount,
            int distinctTitles)
        {
            BorrowerId = borrowerId;
            FullName = fullName;
            LoanCount = loanCount;
            DistinctTitles = distinctTitles;
        }

        public int BorrowerId { get; }

        public string FullName { get; }

        public int LoanCount { get; }

        public int DistinctTitles { get; }
    }

    public sealed class ReadingPaceView
    {
        public ReadingPaceView(int borrowerId, string fullName, ReadingPaceEstimate estimate)
        {
            BorrowerId = borrowerId;
            FullName = fullName;
            Estimate = estimate;
        }

        public int BorrowerId { get; }

        public string FullName { get; }

        public ReadingPaceEstimate Estimate { get; }
    }

    public sealed class AlsoBorrowedBookView
    {
        public AlsoBorrowedBookView(
            int bookId,
            string title,
            string author,
            int sharedBorrowers,
            int loanCount)
        {
            BookId = bookId;
            Title = title;
            Author = author;
            SharedBorrowers = sharedBorrowers;
            LoanCount = loanCount;
        }

        public int BookId { get; }

        public string Title { get; }

        public string Author { get; }

        public int SharedBorrowers { get; }

        public int LoanCount { get; }
    }

    public sealed class AlsoBorrowedView
    {
        public AlsoBorrowedView(
            int bookId,
            string title,
            int cohortSize,
            IReadOnlyList<AlsoBorrowedBookView> books)
        {
            BookId = bookId;
            Title = title;
            CohortSize = cohortSize;
            Books = books;
        }

        public int BookId { get; }

        public string Title { get; }

        public int CohortSize { get; }

        public IReadOnlyList<AlsoBorrowedBookView> Books { get; }
    }
}
