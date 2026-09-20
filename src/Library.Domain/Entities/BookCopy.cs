namespace Library.Domain.Entities
{
    /// <summary>Identity only: a loan points at a physical item, so one open loan per copy is enforceable.</summary>
    public sealed class BookCopy
    {
        private BookCopy()
        {
        }

        public int Id { get; private set; }

        public int BookId { get; private set; }

        public Book? Book { get; private set; }

        internal static BookCopy For(Book book)
        {
            return new BookCopy { Book = book };
        }
    }
}
