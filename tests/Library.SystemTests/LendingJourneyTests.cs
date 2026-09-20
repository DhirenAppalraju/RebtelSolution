using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Library.SystemTests
{
    /// <summary>Full flows: HTTP to gRPC to the database and back.</summary>
    [Collection(nameof(LibraryWriteCollection))]
    public class LendingJourneyTests
    {
        private readonly LibrarySystem _system;

        public LendingJourneyTests(LibrarySystem system)
        {
            _system = system;
        }

        private const int Neuromancer = 3;
        private const int Refactoring = 6;

        [Fact]
        public async Task BorrowReadAndReturn_MovesTheBorrowersReadingPaceByTheExpectedAmount()
        {
            var borrower = await AddBorrower("Pace Reader");

            var loan = await Borrow(borrower, Neuromancer);
            loan.GetProperty("bookTitle").GetString().ShouldBe("Neuromancer");
            loan.GetProperty("returnedAt").ValueKind.ShouldBe(JsonValueKind.Null);

            var before = await Json($"/api/borrowers/{borrower}/reading-pace");
            before.GetProperty("pagesPerDay").ValueKind.ShouldBe(JsonValueKind.Null);
            before.GetProperty("completedLoans").GetInt32().ShouldBe(0);

            _system.Clock.Advance(TimeSpan.FromDays(3));
            await Return(loan.GetProperty("id").GetInt32());

            var after = await Json($"/api/borrowers/{borrower}/reading-pace");
            after.GetProperty("completedLoans").GetInt32().ShouldBe(1);
            after.GetProperty("totalPages").GetInt32().ShouldBe(250);
            after.GetProperty("totalDays").GetInt32().ShouldBe(3);
            after.GetProperty("pagesPerDay").GetDouble().ShouldBe(250.0 / 3, 0.01);
        }

        [Fact]
        public async Task TheOnlyCopy_IsLentOnceUntilItComesBack()
        {
            var first = await AddBorrower("First In Line");
            var second = await AddBorrower("Second In Line");

            var loan = await Borrow(first, Refactoring);

            var refused = await _system.Client.PostAsJsonAsync(
                "/api/loans", new { borrowerId = second, bookId = Refactoring });
            refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            refused.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

            await Return(loan.GetProperty("id").GetInt32());

            var granted = await _system.Client.PostAsJsonAsync(
                "/api/loans", new { borrowerId = second, bookId = Refactoring });
            granted.StatusCode.ShouldBe(HttpStatusCode.Created);

            var reissued = await granted.Content.ReadFromJsonAsync<JsonElement>();
            await Return(reissued.GetProperty("id").GetInt32());
        }

        [Fact]
        public async Task ReturningTwice_IsOkThenConflict()
        {
            var borrower = await AddBorrower("Forgetful Reader");
            var loan = await Borrow(borrower, Neuromancer);
            var loanId = loan.GetProperty("id").GetInt32();

            (await _system.Client.PostAsync($"/api/loans/{loanId}/return", null))
                .StatusCode.ShouldBe(HttpStatusCode.OK);

            (await _system.Client.PostAsync($"/api/loans/{loanId}/return", null))
                .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task ANewCoBorrowingPattern_ShowsUpInAlsoBorrowed()
        {
            var source = await AddBook("Signal Title", 2);
            var companion = await AddBook("Companion Title", 2);

            foreach (var name in new string[] { "Cohort One", "Cohort Two" })
            {
                var borrower = await AddBorrower(name);
                var sourceLoan = await Borrow(borrower, source);
                var companionLoan = await Borrow(borrower, companion);
                await Return(sourceLoan.GetProperty("id").GetInt32());
                await Return(companionLoan.GetProperty("id").GetInt32());
            }

            var result = await Json($"/api/books/{source}/also-borrowed?limit=5");

            result.GetProperty("cohortSize").GetInt32().ShouldBe(2);
            var top = result.GetProperty("books")[0];
            top.GetProperty("title").GetString().ShouldBe("Companion Title");
            top.GetProperty("sharedBorrowers").GetInt32().ShouldBe(2);
        }

        [Fact]
        public async Task BorrowingForAnUnknownBorrower_IsANotFoundProblem()
        {
            var response = await _system.Client.PostAsJsonAsync(
                "/api/loans", new { borrowerId = 99999, bookId = Neuromancer });

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }

        [Fact]
        public async Task AddingABorrowerWithADuplicateEmail_IsAConflict()
        {
            var response = await _system.Client.PostAsJsonAsync(
                "/api/borrowers", new { fullName = "Impostor", email = "ava.chen@example.com" });

            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        private async Task<int> AddBorrower(string name)
        {
            var response = await _system.Client.PostAsJsonAsync(
                "/api/borrowers", new { fullName = name, email = $"{Guid.NewGuid():N}@example.com" });

            response.StatusCode.ShouldBe(HttpStatusCode.Created);

            var created = await response.Content.ReadFromJsonAsync<JsonElement>();
            response.Headers.Location!.ToString().ShouldBe($"/api/borrowers/{created.GetProperty("id").GetInt32()}");
            return created.GetProperty("id").GetInt32();
        }

        private async Task<int> AddBook(string title, int copies)
        {
            var response = await _system.Client.PostAsJsonAsync(
                "/api/books", new { title, author = "A System Test", pageCount = 200, copies });

            response.StatusCode.ShouldBe(HttpStatusCode.Created);

            var created = await response.Content.ReadFromJsonAsync<JsonElement>();
            return created.GetProperty("id").GetInt32();
        }

        private async Task<JsonElement> Borrow(int borrowerId, int bookId)
        {
            var response = await _system.Client.PostAsJsonAsync("/api/loans", new { borrowerId, bookId });
            response.StatusCode.ShouldBe(HttpStatusCode.Created);

            var loan = await response.Content.ReadFromJsonAsync<JsonElement>();
            response.Headers.Location!.ToString().ShouldBe($"/api/loans/{loan.GetProperty("id").GetInt32()}");

            // Location resolves.
            (await _system.Client.GetAsync(response.Headers.Location)).StatusCode.ShouldBe(HttpStatusCode.OK);
            return loan;
        }

        private async Task Return(int loanId)
        {
            (await _system.Client.PostAsync($"/api/loans/{loanId}/return", null))
                .StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        private async Task<JsonElement> Json(string route)
        {
            var response = await _system.Client.GetAsync(route);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {route}");
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
    }
}
