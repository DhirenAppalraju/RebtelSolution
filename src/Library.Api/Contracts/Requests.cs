namespace Library.Api.Contracts
{
    // Settable properties with a default constructor: the shape the JSON model binder expects.
    public sealed class AddBookRequest
    {
        public string? Title { get; set; }

        public string? Author { get; set; }

        public int PageCount { get; set; }

        public int Copies { get; set; }
    }

    public sealed class AddBorrowerRequest
    {
        public string? FullName { get; set; }

        public string? Email { get; set; }
    }

    public sealed class BorrowBookRequest
    {
        public int BorrowerId { get; set; }

        public int BookId { get; set; }
    }
}
