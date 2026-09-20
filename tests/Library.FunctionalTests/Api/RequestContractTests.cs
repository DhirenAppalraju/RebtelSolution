using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using NSubstitute;
using Proto = Library.Contracts.V1;

namespace Library.FunctionalTests.Api
{
    public class RequestContractTests : IClassFixture<ApiHostFixture>
    {
        private readonly ApiHostFixture _fixture;

        public RequestContractTests(ApiHostFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ReadingPace_UnsetPace_IsRealJsonNullNotZero()
        {
            _fixture.Analytics
                .GetReadingPaceAsync(Arg.Any<Proto.ReadingPaceRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Returns(new Proto.ReadingPaceResponse
                {
                    BorrowerId = 7,
                    FullName = "New Member",
                    CompletedLoans = 0,
                }));

            var response = await _fixture.CreateClient().GetAsync("/api/borrowers/7/reading-pace");

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("pagesPerDay").ValueKind.ShouldBe(JsonValueKind.Null);
            body.GetProperty("completedLoans").GetInt32().ShouldBe(0);
        }

        [Fact]
        public async Task ReadingPace_IsRoundedAtTheTransportEdge()
        {
            _fixture.Analytics
                .GetReadingPaceAsync(Arg.Any<Proto.ReadingPaceRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Returns(new Proto.ReadingPaceResponse
                {
                    BorrowerId = 1,
                    FullName = "Ava Chen",
                    PagesPerDay = 102.4567,
                    CompletedLoans = 2,
                }));

            var body = await (await _fixture.CreateClient().GetAsync("/api/borrowers/1/reading-pace"))
                .Content.ReadFromJsonAsync<JsonElement>();

            body.GetProperty("pagesPerDay").GetDouble().ShouldBe(102.46);
        }

        [Fact]
        public async Task Window_IsSentAsUtcMidnightAndLeftUnsetWhenAbsent()
        {
            Proto.MostBorrowedBooksRequest? sent = null;
            _fixture.Analytics
                .GetMostBorrowedBooksAsync(Arg.Do<Proto.MostBorrowedBooksRequest>(r => sent = r), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Returns(new Proto.MostBorrowedBooksResponse()));

            var client = _fixture.CreateClient();

            await client.GetAsync("/api/books/most-borrowed?from=2026-01-01&to=2026-02-01");
            sent!.Period.From.ShouldBe(Timestamp.FromDateTimeOffset(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
            sent.Period.To.ShouldBe(Timestamp.FromDateTimeOffset(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)));

            await client.GetAsync("/api/books/most-borrowed");
            sent!.Period.ShouldBeNull();   // unset means all time
        }

        [Fact]
        public async Task Catalogue_Succeeds_AndReturnsTheBook()
        {
            // The 200 path, previously only exercised through stubbed failures.
            _fixture.Lending
                .GetBookAsync(Arg.Any<Proto.GetBookRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Returns(new Proto.Book
                {
                    Id = 1,
                    Title = "The Hobbit",
                    Author = "J. R. R. Tolkien",
                    PageCount = 300,
                    TotalCopies = 3,
                    AvailableCopies = 2,
                }));

            var response = await _fixture.CreateClient().GetAsync("/api/books/1");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("title").GetString().ShouldBe("The Hobbit");
            body.GetProperty("availableCopies").GetInt32().ShouldBe(2);
        }

        [Fact]
        public async Task Borrower_Succeeds_AndReturnsTheBorrower()
        {
            // Untested at every tier: neither the action nor the gRPC method behind it ran.
            _fixture.Lending
                .GetBorrowerAsync(Arg.Any<Proto.GetBorrowerRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Returns(new Proto.Borrower
                {
                    Id = 3,
                    FullName = "Clara Diaz",
                    Email = "clara.diaz@example.com",
                }));

            var response = await _fixture.CreateClient().GetAsync("/api/borrowers/3");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("fullName").GetString().ShouldBe("Clara Diaz");
            body.GetProperty("email").GetString().ShouldBe("clara.diaz@example.com");
        }

        [Fact]
        public async Task CreatedResources_CarryALocationHeaderThatMatchesTheirId()
        {
            // Asserted for POST /api/loans below, not for the other creating routes.
            // A wrong Location is something only a client notices.
            _fixture.Lending
                .AddBookAsync(Arg.Any<Proto.AddBookRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Returns(new Proto.Book
                {
                    Id = 11, Title = "A New Title", Author = "An Author",
                    PageCount = 123, TotalCopies = 4, AvailableCopies = 4,
                }));

            _fixture.Lending
                .AddBorrowerAsync(Arg.Any<Proto.AddBorrowerRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Returns(new Proto.Borrower
                {
                    Id = 7, FullName = "New Member", Email = "new.member@example.com",
                }));

            var client = _fixture.CreateClient();

            var book = await client.PostAsJsonAsync(
                "/api/books", new { title = "A New Title", author = "An Author", pageCount = 123, copies = 4 });

            book.StatusCode.ShouldBe(HttpStatusCode.Created);
            book.Headers.Location!.ToString().ShouldBe("/api/books/11");
            (await book.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCopies").GetInt32().ShouldBe(4);

            var borrower = await client.PostAsJsonAsync(
                "/api/borrowers", new { fullName = "New Member", email = "new.member@example.com" });

            borrower.StatusCode.ShouldBe(HttpStatusCode.Created);
            borrower.Headers.Location!.ToString().ShouldBe("/api/borrowers/7");
        }

        [Fact]
        public async Task AlsoBorrowed_ForwardsItsWindow()
        {
            // The cohort route's window branch: every previous call omitted `from` and `to`.
            Proto.AlsoBorrowedRequest? sent = null;
            _fixture.Analytics
                .GetAlsoBorrowedBooksAsync(Arg.Do<Proto.AlsoBorrowedRequest>(r => sent = r), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Returns(new Proto.AlsoBorrowedResponse { BookId = 2, Title = "Dune", CohortSize = 5 }));

            var client = _fixture.CreateClient();

            await client.GetAsync("/api/books/2/also-borrowed?from=2026-01-01&to=2026-02-01&limit=3");

            sent!.BookId.ShouldBe(2);
            sent.Limit.ShouldBe(3);
            sent.Period.From.ShouldBe(Timestamp.FromDateTimeOffset(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
            sent.Period.To.ShouldBe(Timestamp.FromDateTimeOffset(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)));

            await client.GetAsync("/api/books/2/also-borrowed");
            sent!.Period.ShouldBeNull();
        }

        [Fact]
        public async Task BorrowBook_ReturnsALocationThatResolves()
        {
            var now = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);
            _fixture.Lending
                .BorrowBookAsync(Arg.Any<Proto.BorrowBookRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Returns(new Proto.Loan
                {
                    Id = 29,
                    BookId = 3,
                    BookTitle = "Neuromancer",
                    BorrowerId = 6,
                    BorrowerName = "Farid Haddad",
                    BorrowedAt = Timestamp.FromDateTimeOffset(now),
                    DueAt = Timestamp.FromDateTimeOffset(now.AddDays(21)),
                }));

            var response = await _fixture.CreateClient()
                .PostAsJsonAsync("/api/loans", new { borrowerId = 6, bookId = 3 });

            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.Headers.Location!.ToString().ShouldBe("/api/loans/29");

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("returnedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        }

        [Fact]
        public async Task Catalogue_CarriesAvailabilityInTheJsonShapeTheReadmeShows()
        {
            var response = new Proto.ListBooksResponse();
            response.Books.Add(new Proto.Book
            {
                Id = 1, Title = "The Hobbit", Author = "J. R. R. Tolkien",
                PageCount = 300, TotalCopies = 3, AvailableCopies = 2,
            });

            _fixture.Lending
                .ListBooksAsync(Arg.Any<Proto.ListBooksRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Returns(response));

            var body = await (await _fixture.CreateClient().GetAsync("/api/books"))
                .Content.ReadFromJsonAsync<JsonElement>();

            var book = body.GetProperty("books")[0];
            book.GetProperty("totalCopies").GetInt32().ShouldBe(3);
            book.GetProperty("availableCopies").GetInt32().ShouldBe(2);
        }
    }
}
