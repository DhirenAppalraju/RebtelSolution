using Grpc.Core;
using Proto = Library.Contracts.V1;

namespace Library.FunctionalTests.Service
{
    /// <summary>Each key feature through the service's own contract.</summary>
    public class LendingFeatureTests : IClassFixture<ServiceHostFixture>
    {
        private readonly ServiceHostFixture _fixture;

        public LendingFeatureTests(ServiceHostFixture fixture)
        {
            _fixture = fixture;
        }

        private const int Hobbit = 1;
        private const int Neuromancer = 3;
        private const int Refactoring = 6;

        [Fact]
        public async Task ListBooks_ReturnsTheCatalogueOrderedByIdWithAvailability()
        {
            var response = await _fixture.Lending.ListBooksAsync(new Proto.ListBooksRequest());

            response.Books.Count.ShouldBe(10);
            response.Books.Select(b => b.Id).ShouldBe(Enumerable.Range(1, 10));
            response.Books.Single(b => b.Title == "Thinking, Fast and Slow").AvailableCopies.ShouldBe(0);
            response.Books.Single(b => b.Title == "Project Hail Mary").AvailableCopies.ShouldBe(1);
        }

        [Fact]
        public async Task BorrowBook_FreeCopy_IsDueTheLoanPeriodAfterTheFrozenClock()
        {
            var borrower = await _fixture.Lending.AddBorrowerAsync(new Proto.AddBorrowerRequest
            {
                FullName = "Borrowing Reader",
                Email = $"borrow-{Guid.NewGuid():N}@example.com",
            });

            var loan = await _fixture.Lending.BorrowBookAsync(
                new Proto.BorrowBookRequest { BorrowerId = borrower.Id, BookId = Neuromancer });

            loan.BookTitle.ShouldBe("Neuromancer");
            loan.BorrowedAt.ToDateTimeOffset().ShouldBe(_fixture.Clock.GetUtcNow());
            loan.DueAt.ToDateTimeOffset().ShouldBe(_fixture.Clock.GetUtcNow().AddDays(21));
            loan.ReturnedAt.ShouldBeNull();

            await _fixture.Lending.ReturnBookAsync(new Proto.ReturnBookRequest { LoanId = loan.Id });
        }

        [Fact]
        public async Task BorrowBook_LastCopyAlreadyOut_IsFailedPrecondition()
        {
            var first = await NewBorrower();
            var second = await NewBorrower();

            var loan = await _fixture.Lending.BorrowBookAsync(
                new Proto.BorrowBookRequest { BorrowerId = first.Id, BookId = Refactoring });

            try
            {
                // Refactoring has one copy.
                var error = await Should.ThrowAsync<RpcException>(() => _fixture.Lending
                    .BorrowBookAsync(new Proto.BorrowBookRequest { BorrowerId = second.Id, BookId = Refactoring })
                    .ResponseAsync);

                error.StatusCode.ShouldBe(StatusCode.FailedPrecondition);
                error.Status.Detail.ShouldContain("Every copy");
            }
            finally
            {
                await _fixture.Lending.ReturnBookAsync(new Proto.ReturnBookRequest { LoanId = loan.Id });
            }
        }

        [Fact]
        public async Task ReturnBook_Twice_IsFailedPrecondition()
        {
            var borrower = await NewBorrower();
            var loan = await _fixture.Lending.BorrowBookAsync(
                new Proto.BorrowBookRequest { BorrowerId = borrower.Id, BookId = Neuromancer });

            var returned = await _fixture.Lending.ReturnBookAsync(new Proto.ReturnBookRequest { LoanId = loan.Id });
            returned.ReturnedAt.ShouldNotBeNull();

            var error = await Should.ThrowAsync<RpcException>(() => _fixture.Lending
                .ReturnBookAsync(new Proto.ReturnBookRequest { LoanId = loan.Id })
                .ResponseAsync);

            error.StatusCode.ShouldBe(StatusCode.FailedPrecondition);
        }

        [Fact]
        public async Task GetBook_ExistingId_ReturnsTheTitle()
        {
            // The returning path: GetBook had only ever been called with a missing id.
            // Reads a seeded title rather than adding one: this class shares a database and has no
            // guaranteed order, so growing the catalogue would race ListBooks' exact count.
            var book = await _fixture.Lending.GetBookAsync(new Proto.GetBookRequest { BookId = Hobbit });

            book.Id.ShouldBe(Hobbit);
            book.Title.ShouldBe("The Hobbit");
            book.Author.ShouldBe("J. R. R. Tolkien");
            book.PageCount.ShouldBe(300);
            book.TotalCopies.ShouldBe(3);
        }

        [Fact]
        public async Task GetBook_UnknownId_IsNotFound()
        {
            var error = await Should.ThrowAsync<RpcException>(() => _fixture.Lending
                .GetBookAsync(new Proto.GetBookRequest { BookId = 9999 })
                .ResponseAsync);

            error.StatusCode.ShouldBe(StatusCode.NotFound);
        }

        [Fact]
        public async Task AddBook_BlankTitle_IsInvalidArgument()
        {
            var error = await Should.ThrowAsync<RpcException>(() => _fixture.Lending
                .AddBookAsync(new Proto.AddBookRequest { Title = "  ", Author = "Someone", PageCount = 10, Copies = 1 })
                .ResponseAsync);

            error.StatusCode.ShouldBe(StatusCode.InvalidArgument);
        }

        [Fact]
        public async Task AddBorrower_DuplicateEmail_IsFailedPrecondition()
        {
            var error = await Should.ThrowAsync<RpcException>(() => _fixture.Lending
                .AddBorrowerAsync(new Proto.AddBorrowerRequest { FullName = "Impostor", Email = "ava.chen@example.com" })
                .ResponseAsync);

            error.StatusCode.ShouldBe(StatusCode.FailedPrecondition);
        }

        [Fact]
        public async Task GetBorrower_RoundTripsWhatWasAdded()
        {
            // Untested at every tier: only an OpenAPI assertion mentioned the route.
            var added = await NewBorrower();

            var fetched = await _fixture.Lending.GetBorrowerAsync(
                new Proto.GetBorrowerRequest { BorrowerId = added.Id });

            fetched.Id.ShouldBe(added.Id);
            fetched.FullName.ShouldBe(added.FullName);
            fetched.Email.ShouldBe(added.Email);
        }

        [Fact]
        public async Task GetBorrower_UnknownId_IsNotFound()
        {
            var error = await Should.ThrowAsync<RpcException>(() => _fixture.Lending
                .GetBorrowerAsync(new Proto.GetBorrowerRequest { BorrowerId = 9999 })
                .ResponseAsync);

            error.StatusCode.ShouldBe(StatusCode.NotFound);
            error.Status.Detail.ShouldContain("9999");
        }

        private async Task<Proto.Borrower> NewBorrower()
        {
            return await _fixture.Lending.AddBorrowerAsync(new Proto.AddBorrowerRequest
            {
                FullName = "Test Reader",
                Email = $"reader-{Guid.NewGuid():N}@example.com",
            });
        }
    }
}
