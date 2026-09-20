namespace Library.Api.Contracts
{
    // Public JSON, deliberately separate from the generated protobuf messages: generated classes carry
    // machinery that has no place in a public contract, `optional` fields become real nulls, and the two
    // contracts can evolve independently.

    public sealed class BookResponse
    {
        public BookResponse(
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

    public sealed class BookListResponse
    {
        public BookListResponse(IReadOnlyList<BookResponse> books)
        {
            Books = books;
        }

        public IReadOnlyList<BookResponse> Books { get; }
    }

    public sealed class BorrowerResponse
    {
        public BorrowerResponse(int id, string fullName, string email)
        {
            Id = id;
            FullName = fullName;
            Email = email;
        }

        public int Id { get; }

        public string FullName { get; }

        public string Email { get; }
    }

    public sealed class LoanResponse
    {
        public LoanResponse(
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

    public sealed class MostBorrowedBookResponse
    {
        public MostBorrowedBookResponse(
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

    public sealed class MostBorrowedBooksResponse
    {
        public MostBorrowedBooksResponse(IReadOnlyList<MostBorrowedBookResponse> books)
        {
            Books = books;
        }

        public IReadOnlyList<MostBorrowedBookResponse> Books { get; }
    }

    public sealed class TopBorrowerResponse
    {
        public TopBorrowerResponse(
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

    public sealed class TopBorrowersResponse
    {
        public TopBorrowersResponse(IReadOnlyList<TopBorrowerResponse> borrowers)
        {
            Borrowers = borrowers;
        }

        public IReadOnlyList<TopBorrowerResponse> Borrowers { get; }
    }

    public sealed class LoanPaceResponse
    {
        public LoanPaceResponse(
            int bookId,
            string title,
            int pages,
            int days,
            double pagesPerDay)
        {
            BookId = bookId;
            Title = title;
            Pages = pages;
            Days = days;
            PagesPerDay = pagesPerDay;
        }

        public int BookId { get; }

        public string Title { get; }

        public int Pages { get; }

        public int Days { get; }

        public double PagesPerDay { get; }
    }

    public sealed class ReadingPaceResponse
    {
        public ReadingPaceResponse(
            int borrowerId,
            string fullName,
            double? pagesPerDay,
            int completedLoans,
            int totalPages,
            int totalDays,
            IReadOnlyList<LoanPaceResponse> loans)
        {
            BorrowerId = borrowerId;
            FullName = fullName;
            PagesPerDay = pagesPerDay;
            CompletedLoans = completedLoans;
            TotalPages = totalPages;
            TotalDays = totalDays;
            Loans = loans;
        }

        public int BorrowerId { get; }

        public string FullName { get; }

        public double? PagesPerDay { get; }

        public int CompletedLoans { get; }

        public int TotalPages { get; }

        public int TotalDays { get; }

        public IReadOnlyList<LoanPaceResponse> Loans { get; }
    }

    public sealed class AlsoBorrowedBookResponse
    {
        public AlsoBorrowedBookResponse(
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

    public sealed class AlsoBorrowedResponse
    {
        public AlsoBorrowedResponse(
            int bookId,
            string title,
            int cohortSize,
            IReadOnlyList<AlsoBorrowedBookResponse> books)
        {
            BookId = bookId;
            Title = title;
            CohortSize = cohortSize;
            Books = books;
        }

        public int BookId { get; }

        public string Title { get; }

        public int CohortSize { get; }

        public IReadOnlyList<AlsoBorrowedBookResponse> Books { get; }
    }
}
