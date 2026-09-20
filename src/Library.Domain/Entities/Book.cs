using Library.Domain.Exceptions;

namespace Library.Domain.Entities
{
    public sealed class Book
    {
        private readonly List<BookCopy> _copies = new List<BookCopy>();

        private Book()
        {
        }

        public int Id { get; private set; }

        public string Title { get; private set; } = string.Empty;

        public string Author { get; private set; } = string.Empty;

        public int PageCount { get; private set; }

        public IReadOnlyCollection<BookCopy> Copies
        {
            get { return _copies; }
        }

        /// <summary>Book plus its copies; a title with no copy cannot be lent.</summary>
        public static Book Create(string title, string author, int pageCount, int copies)
        {
            var book = new Book
            {
                Title = Require(title, nameof(title)),
                Author = Require(author, nameof(author)),
                PageCount = Pages(pageCount),
            };

            if (copies < 1 || copies > 100)
            {
                throw new ValidationException("'copies' must be between 1 and 100.");
            }

            for (var i = 0; i < copies; i++)
            {
                book._copies.Add(BookCopy.For(book));
            }

            return book;
        }

        // Upper bound: reading pace sums pages with checked arithmetic, so an absurd
        // count overflows a later, unrelated request.
        private static int Pages(int pageCount)
        {
            if (pageCount < 1 || pageCount > 50000)
            {
                throw new ValidationException("'pageCount' must be between 1 and 50000.");
            }

            return pageCount;
        }

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ValidationException($"'{name}' is required.");
            }

            return value.Length <= 200
                ? value.Trim()
                : throw new ValidationException($"'{name}' must be 200 characters or fewer.");
        }
    }
}
