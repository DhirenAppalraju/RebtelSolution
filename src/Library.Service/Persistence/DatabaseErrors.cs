using Library.Domain.Exceptions;
using Library.Service.Persistence.Configurations;
using Microsoft.Data.Sqlite;

namespace Library.Service.Persistence
{
    /// <summary>Provider errors recognised in one place.</summary>
    public static class DatabaseErrors
    {
        private const int SqliteConstraintUnique = 2067;

        /// <summary>Lost race to a 409, or null if the error is not ours.</summary>
        public static ConflictException? AsConflict(Exception exception)
        {
            var sqlite = Find(exception);
            if (sqlite == null || sqlite.SqliteExtendedErrorCode != SqliteConstraintUnique)
            {
                return null;
            }

            var message = sqlite.Message;

            if (message.Contains(LoanConfiguration.OpenLoanPerCopyIndex, StringComparison.Ordinal)
                || message.Contains("Loans.BookCopyId", StringComparison.Ordinal))
            {
                return new ConflictException("That copy was taken a moment ago; try again.");
            }

            if (message.Contains(LoanConfiguration.OpenTitlePerBorrowerIndex, StringComparison.Ordinal)
                || message.Contains("Loans.BorrowerId", StringComparison.Ordinal))
            {
                return new ConflictException("That borrower already holds a copy of this title.");
            }

            if (message.Contains("UX_Borrowers_Email", StringComparison.Ordinal)
                || message.Contains("Borrowers.Email", StringComparison.Ordinal))
            {
                return new ConflictException("A borrower with that email already exists.");
            }

            return new ConflictException("The change conflicts with a record that already exists.");
        }

        private static SqliteException? Find(Exception? exception)
        {
            while (exception != null)
            {
                var sqlite = exception as SqliteException;
                if (sqlite != null)
                {
                    return sqlite;
                }

                exception = exception.InnerException;
            }

            return null;
        }
    }
}
