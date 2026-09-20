using Library.Domain.Exceptions;

namespace Library.Domain.Entities
{
    /// <summary>ReturnedAt IS NULL is the state machine; there is no status enum to drift out of step with it.</summary>
    public sealed class Loan
    {
        private Loan()
        {
        }

        public int Id { get; private set; }

        public int BookCopyId { get; private set; }

        public BookCopy? BookCopy { get; private set; }

        // Denormalised from BookCopy: every report groups by title, and EF cannot translate
        // an aggregate whose operand arrives through a join inside a GroupBy. A copy belongs to one title for life.
        public int BookId { get; private set; }

        public Book? Book { get; private set; }

        public int BorrowerId { get; private set; }

        public Borrower? Borrower { get; private set; }

        public DateTimeOffset BorrowedAt { get; private set; }

        public DateTimeOffset DueAt { get; private set; }

        public DateTimeOffset? ReturnedAt { get; private set; }

        public bool IsOpen
        {
            get { return !ReturnedAt.HasValue; }
        }

        public static Loan Open(int bookCopyId, int bookId, int borrowerId, DateTimeOffset borrowedAt, DateTimeOffset dueAt)
        {
            return new Loan
            {
                BookCopyId = bookCopyId,
                BookId = bookId,
                BorrowerId = borrowerId,
                BorrowedAt = borrowedAt,
                DueAt = dueAt,
            };
        }

        public void Return(DateTimeOffset now)
        {
            if (ReturnedAt.HasValue)
            {
                throw new ConflictException($"Loan {Id} was already returned on {ReturnedAt:u}.");
            }

            if (now < BorrowedAt)
            {
                throw new ConflictException($"Loan {Id} cannot be returned before it was borrowed.");
            }

            ReturnedAt = now;
        }
    }
}
