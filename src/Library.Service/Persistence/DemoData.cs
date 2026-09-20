using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Service.Persistence
{
    /// <summary>
    /// Hand-designed, not random: every question has an answer a reviewer can check on paper, and the
    /// README publishes those answers. Idempotent, so it is safe on every start-up.
    /// </summary>
    public static class DemoData
    {
        private const int LoanPeriodDays = 21;

        public static void Seed(LibraryDbContext db)
        {
            if (db.Books.Any())
            {
                return;
            }

            Populate(db);
            db.SaveChanges();
        }

        public static async Task SeedAsync(LibraryDbContext db, CancellationToken ct = default)
        {
            if (await db.Books.AnyAsync(ct))
            {
                return;
            }

            Populate(db);
            await db.SaveChangesAsync(ct);
        }

        private static void Populate(LibraryDbContext db)
        {
            AddBooks(db);
            AddBorrowers(db);
            AddLoans(db);
        }

        // Page counts are round approximations for the demo.
        private static void AddBooks(LibraryDbContext db)
        {
            (int Id, string Title, string Author, int Pages, int Copies)[] books = new (int Id, string Title, string Author, int Pages, int Copies)[]
            {
                (1, "The Hobbit", "J. R. R. Tolkien", 300, 3),
                (2, "Dune", "Frank Herbert", 600, 2),
                (3, "Neuromancer", "William Gibson", 250, 1),
                (4, "Sapiens", "Yuval Noah Harari", 450, 2),
                (5, "Clean Code", "Robert C. Martin", 400, 2),
                (6, "Refactoring", "Martin Fowler", 420, 1),
                (7, "Project Hail Mary", "Andy Weir", 480, 2),
                (8, "Thinking, Fast and Slow", "Daniel Kahneman", 500, 1),
                (9, "The Left Hand of Darkness", "Ursula K. Le Guin", 300, 1),
                (10, "Quiet", "Susan Cain", 350, 1),
            };

            var copyId = 1;
            foreach (var (id, title, author, pages, copies) in books)
            {
                var book = Book.Create(title, author, pages, copies);
                db.Books.Add(book);
                SetId(db, book, id);

                // SQLite accepts explicit values for INTEGER PRIMARY KEY; the ids are asserted by a test.
                foreach (var copy in book.Copies)
                {
                    SetId(db, copy, copyId++);
                }
            }
        }

        private static void AddBorrowers(LibraryDbContext db)
        {
            (int Id, string Name, string Email)[] borrowers = new (int Id, string Name, string Email)[]
            {
                (1, "Ava Chen", "ava.chen@example.com"),
                (2, "Ben Okafor", "ben.okafor@example.com"),
                (3, "Clara Diaz", "clara.diaz@example.com"),
                (4, "Dmitri Volkov", "dmitri.volkov@example.com"),
                (5, "Elena Rossi", "elena.rossi@example.com"),
                (6, "Farid Haddad", "farid.haddad@example.com"),
            };

            foreach (var (id, name, email) in borrowers)
            {
                var borrower = Borrower.Create(name, email);
                db.Borrowers.Add(borrower);
                SetId(db, borrower, id);
            }
        }

        private static void AddLoans(LibraryDbContext db)
        {
            // Id, borrower, book, copy, borrowed (month, day, hour), returned (null = still out).
            (int Id, int Borrower, int Book, int Copy, (int M, int D, int H) From, (int M, int D, int H)? To)[] loans = new (int Id, int Borrower, int Book, int Copy, (int M, int D, int H) From, (int M, int D, int H)? To)[]
            {
                (1, 6, 1, 3, (1, 2, 10), (1, 4, 10)),
                (2, 1, 5, 9, (1, 2, 10), (1, 6, 10)),
                (3, 2, 1, 1, (1, 3, 10), (1, 6, 10)),
                (4, 6, 5, 10, (1, 3, 10), (1, 9, 10)),
                (5, 1, 6, 11, (1, 4, 10), (1, 10, 10)),
                (6, 3, 1, 2, (1, 5, 10), (1, 8, 10)),
                (7, 2, 2, 4, (1, 8, 10), (1, 15, 10)),
                (8, 3, 2, 5, (1, 10, 10), (1, 16, 10)),
                (9, 2, 3, 6, (1, 20, 10), (1, 22, 10)),
                (10, 1, 1, 1, (1, 20, 10), (1, 23, 10)),
                (11, 1, 2, 4, (1, 24, 10), (1, 30, 10)),
                (12, 3, 3, 6, (2, 1, 10), (2, 1, 16)),   // six hours rounds up to a day
                (13, 1, 4, 7, (2, 2, 10), (2, 8, 10)),
                (14, 4, 2, 4, (2, 3, 10), (2, 12, 10)),
                (15, 1, 5, 9, (2, 10, 10), (2, 14, 10)),
                (16, 4, 1, 1, (2, 14, 10), (2, 17, 10)),
                (17, 4, 2, 4, (2, 20, 10), (2, 27, 10)),
                (18, 2, 4, 7, (3, 1, 10), (3, 7, 10)),
                (19, 4, 3, 6, (3, 1, 10), (3, 3, 10)),
                (20, 5, 2, 4, (3, 2, 10), (3, 10, 10)),
                (21, 5, 4, 8, (3, 3, 10), (3, 9, 10)),
                (22, 5, 1, 1, (3, 12, 10), (3, 15, 10)),
                (23, 5, 9, 15, (4, 1, 10), (4, 5, 10)),  // 1 April: excluded by an exclusive to=2026-04-01
                (24, 1, 7, 12, (4, 3, 10), (4, 9, 10)),
                (25, 1, 8, 14, (4, 12, 10), (4, 20, 10)),
                (26, 6, 8, 14, (5, 1, 10), null),
                (27, 2, 10, 16, (5, 2, 10), (5, 9, 10)),
                (28, 3, 7, 12, (6, 1, 10), null),
            };

            foreach (var (id, borrowerId, bookId, copyId, from, to) in loans)
            {
                var borrowedAt = Utc(from);
                var loan = Loan.Open(copyId, bookId, borrowerId, borrowedAt, borrowedAt.AddDays(LoanPeriodDays));

                if (to.HasValue)
                {
                    loan.Return(Utc(to.Value));
                }

                db.Loans.Add(loan);
                SetId(db, loan, id);
            }
        }

        private static DateTimeOffset Utc((int Month, int Day, int Hour) at)
        {
            return new DateTimeOffset(2026, at.Month, at.Day, at.Hour, 0, 0, TimeSpan.Zero);
        }

        private static void SetId<T>(LibraryDbContext db, T entity, int id)
            where T : class
        {
            db.Entry(entity).Property("Id").CurrentValue = id;
        }
    }
}
