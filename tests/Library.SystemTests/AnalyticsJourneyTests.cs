using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grpc.Health.V1;
using Grpc.Net.Client;

namespace Library.SystemTests
{
    /// <summary>The four questions over HTTP, through both hosts, against published answers.</summary>
    [Collection(nameof(LibrarySystemCollection))]
    public class AnalyticsJourneyTests
    {
        private readonly LibrarySystem _system;

        public AnalyticsJourneyTests(LibrarySystem system)
        {
            _system = system;
        }

        [Fact]
        public async Task Q1_MostBorrowedBooks_MatchesThePublishedRanking()
        {
            var books = (await Json("/api/books/most-borrowed?limit=5")).GetProperty("books");

            books.GetArrayLength().ShouldBe(5);
            books[0].GetProperty("title").GetString().ShouldBe("The Hobbit");
            books[0].GetProperty("borrowCount").GetInt32().ShouldBe(6);
            books[0].GetProperty("distinctBorrowers").GetInt32().ShouldBe(6);
            books[1].GetProperty("title").GetString().ShouldBe("Dune");
            books[1].GetProperty("distinctBorrowers").GetInt32().ShouldBe(5);
        }

        [Fact]
        public async Task Q2_TopBorrowers_MatchesThePublishedRanking()
        {
            var borrowers = (await Json("/api/borrowers/top?from=2026-01-01&to=2026-04-01"))
                .GetProperty("borrowers");

            borrowers[0].GetProperty("fullName").GetString().ShouldBe("Ava Chen");
            borrowers[0].GetProperty("loanCount").GetInt32().ShouldBe(6);
            borrowers[0].GetProperty("distinctTitles").GetInt32().ShouldBe(5);
            borrowers[1].GetProperty("fullName").GetString().ShouldBe("Ben Okafor");
            borrowers[2].GetProperty("fullName").GetString().ShouldBe("Dmitri Volkov");
        }

        [Fact]
        public async Task Q2_ExclusiveUpperBound_IsVisibleOverHttp()
        {
            var excluded = (await Json("/api/borrowers/top?from=2026-01-01&to=2026-04-01")).GetProperty("borrowers");
            var included = (await Json("/api/borrowers/top?from=2026-01-01&to=2026-04-02")).GetProperty("borrowers");

            Loans(excluded, "Elena Rossi").ShouldBe(3);
            Loans(included, "Elena Rossi").ShouldBe(4);

            int Loans(JsonElement borrowers, string name)
            {
                return borrowers.EnumerateArray()
                    .Single(b => b.GetProperty("fullName").GetString() == name)
                    .GetProperty("loanCount").GetInt32();
            }
        }

        [Fact]
        public async Task Q2_WithoutAWindow_IsABadRequest()
        {
            var response = await _system.Client.GetAsync("/api/borrowers/top");

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }

        [Fact]
        public async Task Q3_ReadingPace_ReproducesTheReadmesNumberForClara()
        {
            var pace = await Json("/api/borrowers/3/reading-pace");

            pace.GetProperty("fullName").GetString().ShouldBe("Clara Diaz");
            pace.GetProperty("pagesPerDay").GetDouble().ShouldBe(115.0);
            pace.GetProperty("completedLoans").GetInt32().ShouldBe(3);
            pace.GetProperty("totalPages").GetInt32().ShouldBe(1150);
            pace.GetProperty("totalDays").GetInt32().ShouldBe(10);
            pace.GetProperty("loans").GetArrayLength().ShouldBe(3);
        }

        [Fact]
        public async Task Q3_OverlappingLoansAreNotNetted()
        {
            var pace = await Json("/api/borrowers/1/reading-pace?from=2026-01-01&to=2026-01-12");

            // Merged intervals would give 102.5.
            pace.GetProperty("pagesPerDay").GetDouble().ShouldBe(82.0);
        }

        [Fact]
        public async Task Q4_AlsoBorrowed_MatchesThePublishedCohortAndRanking()
        {
            var result = await Json("/api/books/2/also-borrowed?limit=3");

            result.GetProperty("title").GetString().ShouldBe("Dune");
            result.GetProperty("cohortSize").GetInt32().ShouldBe(5);

            var books = result.GetProperty("books");
            books[0].GetProperty("title").GetString().ShouldBe("The Hobbit");
            books[0].GetProperty("sharedBorrowers").GetInt32().ShouldBe(5);
            books[1].GetProperty("title").GetString().ShouldBe("Neuromancer");
            books[2].GetProperty("title").GetString().ShouldBe("Sapiens");
        }

        [Fact]
        public async Task Catalogue_ShowsThePublishedAvailability()
        {
            var books = (await Json("/api/books")).GetProperty("books");

            books.GetArrayLength().ShouldBe(10);
            Availability("Thinking, Fast and Slow").ShouldBe((1, 0));
            Availability("Project Hail Mary").ShouldBe((2, 1));
            Availability("The Hobbit").ShouldBe((3, 3));

            (int Total, int Available) Availability(string title)
            {
                var book = books.EnumerateArray().Single(b => b.GetProperty("title").GetString() == title);
                return (book.GetProperty("totalCopies").GetInt32(), book.GetProperty("availableCopies").GetInt32());
            }
        }

        [Fact]
        public async Task UnknownBook_IsAProblemDocument()
        {
            var response = await _system.Client.GetAsync("/api/books/9999");

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }

        [Fact]
        public async Task Health_IsUpOnBothHosts()
        {
            // API liveness; never calls the service.
            (await _system.Client.GetAsync("/health")).StatusCode.ShouldBe(HttpStatusCode.OK);

            // And the service's, the standard gRPC health service rather than an HTTP route.
            using (var channel = GrpcChannel.ForAddress(_system.ServiceAddress))
            {
                var health = new Health.HealthClient(channel);

                var response = await health.CheckAsync(new HealthCheckRequest());

                response.Status.ShouldBe(HealthCheckResponse.Types.ServingStatus.Serving);
            }
        }

        private async Task<JsonElement> Json(string route)
        {
            var response = await _system.Client.GetAsync(route);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {route}");
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
    }
}
