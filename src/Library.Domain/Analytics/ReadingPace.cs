namespace Library.Domain.Analytics
{
    public readonly struct CompletedLoan
    {
        public CompletedLoan(
            int bookId,
            string title,
            int pages,
            DateTimeOffset borrowedAt,
            DateTimeOffset returnedAt)
        {
            BookId = bookId;
            Title = title;
            Pages = pages;
            BorrowedAt = borrowedAt;
            ReturnedAt = returnedAt;
        }

        public int BookId { get; }

        public string Title { get; }

        public int Pages { get; }

        public DateTimeOffset BorrowedAt { get; }

        public DateTimeOffset ReturnedAt { get; }
    }

    public sealed class LoanPace
    {
        public LoanPace(int bookId, string title, int pages, int days, double pagesPerDay)
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

    public sealed class ReadingPaceEstimate
    {
        private static readonly ReadingPaceEstimate Empty =
            new ReadingPaceEstimate(null, 0, 0, 0, Array.Empty<LoanPace>());

        public ReadingPaceEstimate(
            double? pagesPerDay,
            int completedLoans,
            int totalPages,
            int totalDays,
            IReadOnlyList<LoanPace> loans)
        {
            PagesPerDay = pagesPerDay;
            CompletedLoans = completedLoans;
            TotalPages = totalPages;
            TotalDays = totalDays;
            Loans = loans;
        }

        public static ReadingPaceEstimate None
        {
            get { return Empty; }
        }

        public double? PagesPerDay { get; }

        public int CompletedLoans { get; }

        public int TotalPages { get; }

        public int TotalDays { get; }

        public IReadOnlyList<LoanPace> Loans { get; }
    }

    /// <summary>Pure: no I/O, no clock. Continuous reading, per book.</summary>
    public static class ReadingPaceCalculator
    {
        /// <summary>Whole days, rounded up, minimum 1: no divide-by-zero.</summary>
        public static int WholeDays(DateTimeOffset borrowedAt, DateTimeOffset returnedAt)
        {
            var span = returnedAt - borrowedAt;
            if (span < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(returnedAt), "Return precedes borrow.");
            }

            return Math.Max(1, (int)Math.Ceiling(span.TotalDays));
        }

        public static ReadingPaceEstimate Estimate(IReadOnlyList<CompletedLoan> loans)
        {
            ArgumentNullException.ThrowIfNull(loans);

            if (loans.Count == 0)
            {
                return ReadingPaceEstimate.None;
            }

            var paces = loans
                .OrderBy(l => l.BorrowedAt)
                .Select(l =>
                {
                    var days = WholeDays(l.BorrowedAt, l.ReturnedAt);
                    return new LoanPace(l.BookId, l.Title, l.Pages, days, (double)l.Pages / days);
                })
                .ToArray();

            var pages = paces.Sum(p => p.Pages);
            var days = paces.Sum(p => p.Days);

            // Sum over sum, not mean of paces: weights by length, ignores blips.
            return new ReadingPaceEstimate((double)pages / days, paces.Length, pages, days, paces);
        }
    }
}
