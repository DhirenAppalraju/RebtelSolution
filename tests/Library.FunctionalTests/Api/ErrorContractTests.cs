using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grpc.Core;
using NSubstitute;
using Proto = Library.Contracts.V1;

namespace Library.FunctionalTests.Api
{
    public class ErrorContractTests : IClassFixture<ApiHostFixture>
    {
        private readonly ApiHostFixture _fixture;

        public ErrorContractTests(ApiHostFixture fixture)
        {
            _fixture = fixture;
        }

        public static TheoryData<StatusCode, int> StatusMatrix
        {
            get
            {
                return new TheoryData<StatusCode, int>
                {
                    { StatusCode.InvalidArgument, StatusCodes.Status400BadRequest },
                    { StatusCode.NotFound, StatusCodes.Status404NotFound },
                    { StatusCode.FailedPrecondition, StatusCodes.Status409Conflict },
                    { StatusCode.AlreadyExists, StatusCodes.Status409Conflict },
                    { StatusCode.Aborted, StatusCodes.Status409Conflict },
                    { StatusCode.Cancelled, StatusCodes.Status499ClientClosedRequest },
                    { StatusCode.Unavailable, StatusCodes.Status503ServiceUnavailable },
                    { StatusCode.DeadlineExceeded, StatusCodes.Status504GatewayTimeout },
                    { StatusCode.Internal, StatusCodes.Status500InternalServerError },
                    { StatusCode.Unknown, StatusCodes.Status500InternalServerError },
                };
            }
        }

        [Theory]
        [MemberData(nameof(StatusMatrix))]
        public async Task ServiceStatus_MapsToItsHttpStatusAsAProblemDocument(StatusCode code, int expected)
        {
            _fixture.Lending
                .GetBookAsync(Arg.Any<Proto.GetBookRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.Book>(code));

            var response = await _fixture.CreateClient().GetAsync("/api/books/1");

            ((int)response.StatusCode).ShouldBe(expected);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }

        [Fact]
        public async Task ExpectedFailure_CarriesTheServicesOwnDetail()
        {
            _fixture.Lending
                .GetBookAsync(Arg.Any<Proto.GetBookRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.Book>(StatusCode.NotFound, "Book 42 was not found."));

            var response = await _fixture.CreateClient().GetAsync("/api/books/42");

            (await Detail(response)).ShouldBe("Book 42 was not found.");
        }

        [Fact]
        public async Task ServiceUnreachable_TellsTheReviewerWhatToStart()
        {
            _fixture.Lending
                .ListBooksAsync(Arg.Any<Proto.ListBooksRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.ListBooksResponse>(StatusCode.Unavailable));

            var response = await _fixture.CreateClient().GetAsync("/api/books");

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

            var detail = await Detail(response);
            detail.ShouldContain(ApiHostFixture.GrpcEndpoint);
            detail.ShouldContain("dotnet run --project src/Library.Service");
        }

        [Fact]
        public async Task UnexpectedFailure_DoesNotLeakTheCause()
        {
            _fixture.Lending
                .GetLoanAsync(Arg.Any<Proto.GetLoanRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.Loan>(StatusCode.Internal, "connection string: secret"));

            var response = await _fixture.CreateClient().GetAsync("/api/loans/1");

            // Asserting the replacement, not just the absence: a blank detail would also "not
            // contain secret", and would be a worse answer than the generic one.
            var detail = await Detail(response);

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            detail.ShouldBe("An unexpected error occurred.");
            detail.ShouldNotContain("secret");
        }

        [Fact]
        public async Task ServiceTimedOut_TellsTheCallerWhereAndForHowLong()
        {
            // The twin of the 503 test above. The deadline is the mechanism that stops a wedged
            // service holding requests open, so the message has to name both the endpoint and the
            // budget - otherwise a 504 is indistinguishable from any other gateway failure.
            _fixture.Lending
                .ListBooksAsync(Arg.Any<Proto.ListBooksRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.ListBooksResponse>(StatusCode.DeadlineExceeded));

            var response = await _fixture.CreateClient().GetAsync("/api/books");

            response.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);

            var detail = await Detail(response);
            detail.ShouldContain(ApiHostFixture.GrpcEndpoint);
            detail.ShouldContain("10 seconds");
        }

        [Fact]
        public async Task Catalogue_Succeeds_AndReturnsTheBook()
        {
            // GET /api/books/{id} was only ever exercised through stubbed failures, so the 200
            // path - the one every other test assumes works - was never actually run.
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
            // GET /api/borrowers/{id} had no test at any tier: neither the controller action nor
            // the gRPC method behind it executed anywhere in the suite.
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
        public async Task EveryRouteThatDeclaresA404_ActuallyProducesOne()
        {
            // The OpenAPI test asserts these routes *declare* 404. Four of them had no test that
            // the declaration was true. Stub every downstream call as NotFound and walk them.
            var missing = new Status(StatusCode.NotFound, "it does not exist.");

            _fixture.Lending
                .GetBookAsync(Arg.Any<Proto.GetBookRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.Book>(missing.StatusCode, missing.Detail));
            _fixture.Lending
                .GetBorrowerAsync(Arg.Any<Proto.GetBorrowerRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.Borrower>(missing.StatusCode, missing.Detail));
            _fixture.Lending
                .GetLoanAsync(Arg.Any<Proto.GetLoanRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.Loan>(missing.StatusCode, missing.Detail));
            _fixture.Lending
                .ReturnBookAsync(Arg.Any<Proto.ReturnBookRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.Loan>(missing.StatusCode, missing.Detail));
            _fixture.Analytics
                .GetReadingPaceAsync(Arg.Any<Proto.ReadingPaceRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.ReadingPaceResponse>(missing.StatusCode, missing.Detail));
            _fixture.Analytics
                .GetAlsoBorrowedBooksAsync(Arg.Any<Proto.AlsoBorrowedRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.AlsoBorrowedResponse>(missing.StatusCode, missing.Detail));

            var client = _fixture.CreateClient();

            var routes = new[]
            {
                "/api/books/999",
                "/api/borrowers/999",
                "/api/loans/999",
                "/api/borrowers/999/reading-pace",
                "/api/books/999/also-borrowed",
            };

            foreach (var route in routes)
            {
                var response = await client.GetAsync(route);

                response.StatusCode.ShouldBe(HttpStatusCode.NotFound, route);
                response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json", route);
            }

            var returned = await client.PostAsync("/api/loans/999/return", content: null);

            returned.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            returned.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }

        [Fact]
        public async Task NonNumericRouteId_IsA404FromRoutingBeforeAnyCall()
        {
            var response = await _fixture.CreateClient().GetAsync("/api/books/not-a-number");

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task UnmatchedRoute_IsStillAProblemDocument()
        {
            var response = await _fixture.CreateClient().GetAsync("/api/nothing-here");

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }

        [Fact]
        public async Task Health_AnswersWhileTheServiceIsUnreachable()
        {
            _fixture.Lending
                .ListBooksAsync(Arg.Any<Proto.ListBooksRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.ListBooksResponse>(StatusCode.Unavailable));

            var response = await _fixture.CreateClient().GetAsync("/health");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        private static async Task<string> Detail(HttpResponseMessage response)
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            return problem.GetProperty("detail").GetString() ?? string.Empty;
        }
    }
}
