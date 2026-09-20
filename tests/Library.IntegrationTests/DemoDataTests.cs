using Library.Service.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests
{
    public class DemoDataTests
    {
        [Fact]
        public async Task Seed_AppliesTheFixtureWithTheIdsTheReadmePublishes()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    (await db.Books.CountAsync()).ShouldBe(10);
                    (await db.BookCopies.CountAsync()).ShouldBe(16);
                    (await db.Borrowers.CountAsync()).ShouldBe(6);
                    (await db.Loans.CountAsync()).ShouldBe(28);

                    (await db.Books.SingleAsync(b => b.Id == 1)).Title.ShouldBe("The Hobbit");
                    (await db.Books.SingleAsync(b => b.Id == 2)).Title.ShouldBe("Dune");
                    (await db.Borrowers.SingleAsync(b => b.Id == 3)).FullName.ShouldBe("Clara Diaz");

                    // Copy ids 1..16 in book order: Hobbit 1-3, Dune 4-5, Neuromancer 6.
                    (await db.BookCopies.Where(c => c.BookId == 1).Select(c => c.Id).OrderBy(id => id).ToListAsync())
                        .ShouldBe(new int[] { 1, 2, 3 });
                    (await db.BookCopies.SingleAsync(c => c.Id == 6)).BookId.ShouldBe(3);

                    (await db.Loans.CountAsync(l => l.ReturnedAt == null)).ShouldBe(2);
                }
            }
        }

        [Fact]
        public async Task Seed_RunTwice_IsIdempotent()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    await DemoData.SeedAsync(db);
                    DemoData.Seed(db);

                    (await db.Loans.CountAsync()).ShouldBe(28);
                }
            }
        }

        [Fact]
        public async Task Fixture_ObeysTheConstraintsItIsInsertedUnder()
        {
            using (var database = LibraryDatabase.Seeded())
            {
                await using (var db = database.NewContext())
                {
                    var open = await db.Loans.Where(l => l.ReturnedAt == null).ToListAsync();

                    open.Select(l => l.BookCopyId).Distinct().Count().ShouldBe(open.Count);
                    open.Select(l => (l.BorrowerId, l.BookId)).Distinct().Count().ShouldBe(open.Count);
                    open.GroupBy(l => l.BorrowerId).ShouldAllBe(g => g.Count() <= 5);
                }
            }
        }
    }
}
