using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Grpc.Core;
using NSubstitute;
using Proto = Library.Contracts.V1;

namespace Library.FunctionalTests.Api
{
    /// <summary>
    /// Pins binder behaviour at the edges: absent body, empty query value, repeated parameter.
    /// Each was decided differently under the minimal-API routing this replaced.
    /// </summary>
    public class BindingContractTests : IClassFixture<ApiHostFixture>
    {
        private readonly ApiHostFixture _fixture;

        public BindingContractTests(ApiHostFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [InlineData("/api/books")]
        [InlineData("/api/borrowers")]
        [InlineData("/api/loans")]
        public async Task Post_WithNoBodyAndNoContentType_Is415(string route)
        {
            // No Content-Type: no formatter claims the request, so MVC rejects it before validation.
            // 415 is accurate: the media type is the problem, not the contents.
            var response = await _fixture.CreateClient().PostAsync(route, content: null);

            response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        }

        [Theory]
        [InlineData("/api/books")]
        [InlineData("/api/borrowers")]
        public async Task Post_EmptyJsonObject_ReachesTheServiceAndFailsItsRule(string route)
        {
            // Nullable strings on purpose: a non-nullable one gains an implicit [Required], MVC
            // answers 400 itself, and the service's own validation never runs. This test notices
            // if somebody tidies the `string?` away.
            _fixture.Lending
                .AddBookAsync(Arg.Any<Proto.AddBookRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.Book>(StatusCode.InvalidArgument, "'title' is required."));
            _fixture.Lending
                .AddBorrowerAsync(Arg.Any<Proto.AddBorrowerRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.Borrower>(StatusCode.InvalidArgument, "'fullName' is required."));

            var response = await _fixture.CreateClient().PostAsync(route, Json("{}"));

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            // The service's words, not the framework's: the call was made.
            (await Detail(response)).ShouldContain("is required");
        }

        [Fact]
        public async Task Post_MalformedJson_Is400FromTheBinder()
        {
            _fixture.Lending.ClearReceivedCalls();

            var response = await _fixture.CreateClient().PostAsync("/api/books", Json("{ not json"));

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            _fixture.Lending.ReceivedCalls().ShouldBeEmpty();
        }

        [Fact]
        public async Task EmptyWindowValues_AreTreatedAsAbsentNotAsBadInput()
        {
            // `?from=&to=` binds to null, so the report runs over all time. The minimal-API binder
            // answered 400 instead; empty-as-absent is now a deliberate decision.
            Proto.MostBorrowedBooksRequest? sent = null;
            _fixture.Analytics
                .GetMostBorrowedBooksAsync(Arg.Do<Proto.MostBorrowedBooksRequest>(r => sent = r), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Returns(new Proto.MostBorrowedBooksResponse()));

            var response = await _fixture.CreateClient().GetAsync("/api/books/most-borrowed?from=&to=");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            sent!.Period.ShouldBeNull();
        }

        [Fact]
        public async Task RepeatedQueryParameter_BindsTheFirstValue()
        {
            Proto.MostBorrowedBooksRequest? sent = null;
            _fixture.Analytics
                .GetMostBorrowedBooksAsync(Arg.Do<Proto.MostBorrowedBooksRequest>(r => sent = r), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Returns(new Proto.MostBorrowedBooksResponse()));

            var response = await _fixture.CreateClient().GetAsync("/api/books/most-borrowed?limit=3&limit=9");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            sent!.Limit.ShouldBe(3);
        }

        [Fact]
        public async Task NonNumericLimit_Is400AndNeverReachesTheService()
        {
            _fixture.Analytics.ClearReceivedCalls();

            var response = await _fixture.CreateClient().GetAsync("/api/books/most-borrowed?limit=abc");

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            _fixture.Analytics.ReceivedCalls().ShouldBeEmpty();
        }

        [Fact]
        public async Task NegativeLimit_ReachesTheServiceAndIsRejectedThere()
        {
            // `limit` is an int and binds fine: the cap and sign are the service's rule, enforced once.
            _fixture.Analytics
                .GetMostBorrowedBooksAsync(Arg.Any<Proto.MostBorrowedBooksRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.MostBorrowedBooksResponse>(
                    StatusCode.InvalidArgument, "'limit' must not be negative."));

            var response = await _fixture.CreateClient().GetAsync("/api/books/most-borrowed?limit=-5");

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await Detail(response)).ShouldContain("negative");
        }

        [Theory]
        // Every route that takes a window, in one place; previously duplicated across two files.
        [InlineData("/api/borrowers/top?from=2026-04-01&to=2026-01-01")]
        [InlineData("/api/books/most-borrowed?from=2026-04-01&to=2026-01-01")]
        [InlineData("/api/borrowers/3/reading-pace?from=2026-04-01&to=2026-01-01")]
        [InlineData("/api/books/2/also-borrowed?from=2026-04-01&to=2026-01-01")]
        // Equal bounds count as inverted: [x, x) is empty.
        [InlineData("/api/books/most-borrowed?from=2026-01-01&to=2026-01-01")]
        public async Task InvertedWindow_IsRejectedOnEveryRouteThatTakesOne(string route)
        {
            _fixture.Analytics.ClearReceivedCalls();

            var response = await _fixture.CreateClient().GetAsync(route);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
            _fixture.Analytics.ReceivedCalls().ShouldBeEmpty();
        }

        [Fact]
        public async Task TopBorrowers_WithoutAWindow_Is400AndNeverReachesTheService()
        {
            // The only required window, and the only API-owned rule beyond window shape.
            _fixture.Analytics.ClearReceivedCalls();

            var response = await _fixture.CreateClient().GetAsync("/api/borrowers/top");

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await Detail(response)).ShouldContain("yyyy-MM-dd");
            _fixture.Analytics.ReceivedCalls().ShouldBeEmpty();
        }

        [Fact]
        public async Task SingleBoundWindow_SendsOnlyThatBound()
        {
            Proto.MostBorrowedBooksRequest? sent = null;
            _fixture.Analytics
                .GetMostBorrowedBooksAsync(Arg.Do<Proto.MostBorrowedBooksRequest>(r => sent = r), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Returns(new Proto.MostBorrowedBooksResponse()));

            var client = _fixture.CreateClient();

            await client.GetAsync("/api/books/most-borrowed?from=2026-01-01");
            sent!.Period.ShouldNotBeNull();
            sent.Period.From.ShouldNotBeNull();
            sent.Period.To.ShouldBeNull();

            await client.GetAsync("/api/books/most-borrowed?to=2026-02-01");
            sent!.Period.ShouldNotBeNull();
            sent.Period.From.ShouldBeNull();
            sent.Period.To.ShouldNotBeNull();
        }

        private static StringContent Json(string body)
        {
            return new StringContent(body, Encoding.UTF8, "application/json");
        }

        private static async Task<string> Detail(HttpResponseMessage response)
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            return problem.GetProperty("detail").GetString() ?? string.Empty;
        }
    }
}
