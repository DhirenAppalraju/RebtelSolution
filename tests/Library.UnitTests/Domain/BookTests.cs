using Library.Domain.Entities;
using Library.Domain.Exceptions;

namespace Library.UnitTests.Domain
{
    public class BookTests
    {
        public static TheoryData<string, string, int, int, string> Invalid =>
            new TheoryData<string, string, int, int, string>
            {
                { "", "An Author", 100, 1, "title" },
                { "   ", "An Author", 100, 1, "title" },
                { new string('t', 201), "An Author", 100, 1, "title" },
                { "A Title", "", 100, 1, "author" },
                { "A Title", "   ", 100, 1, "author" },
                { "A Title", new string('a', 201), 100, 1, "author" },
                { "A Title", "An Author", 0, 1, "pageCount" },
                { "A Title", "An Author", -1, 1, "pageCount" },
                // Bound that keeps a reading-pace sum from overflowing.
                { "A Title", "An Author", 50001, 1, "pageCount" },
                { "A Title", "An Author", int.MaxValue, 1, "pageCount" },
                { "A Title", "An Author", 100, 0, "copies" },
                { "A Title", "An Author", 100, -1, "copies" },
                { "A Title", "An Author", 100, 101, "copies" },
            };

        [Theory]
        [MemberData(nameof(Invalid))]
        public void Create_InvalidInput_ThrowsNamingTheField(
            string title, string author, int pageCount, int copies, string field)
        {
            Should.Throw<ValidationException>(() => Book.Create(title, author, pageCount, copies))
                .Message.ShouldContain(field);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(50000)]
        [InlineData(100)]
        public void Create_PageCountAtTheBounds_IsAccepted(int pageCount)
        {
            Book.Create("A Title", "An Author", pageCount, 1).PageCount.ShouldBe(pageCount);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        public void Create_CopiesAtTheBounds_MakesThatManyCopies(int copies)
        {
            Book.Create("A Title", "An Author", 100, copies).Copies.Count.ShouldBe(copies);
        }

        [Fact]
        public void Create_TrimsTitleAndAuthor()
        {
            var book = Book.Create("  The Hobbit  ", "  Tolkien  ", 300, 1);

            book.Title.ShouldBe("The Hobbit");
            book.Author.ShouldBe("Tolkien");
        }

        [Fact]
        public void Create_BornWithItsCopies()
        {
            // A title with no copy cannot be lent: created together.
            var book = Book.Create("A Title", "An Author", 100, 3);

            book.Copies.Count.ShouldBe(3);
            book.Copies.ShouldAllBe(c => c.Book == book);
        }
    }
}
