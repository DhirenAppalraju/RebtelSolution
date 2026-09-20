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
    /// These pin the binder's behaviour at the edges: an absent body, an empty query value, a
    /// repeated parameter. Each of these is a decision the framework makes on the API's behalf,
    /// and each was a different decision under the minimal-API routing this replaced - so they are
    /// written down here rather than left to be discovered by a caller.
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
            // No Content-Type means no input formatter can claim the request, so MVC rejects it
            // before model validation runs. 415 is the accurate answer: the problem is the media
            // type, not the contents.
            var response = await _fixture.CreateClient().PostAsync(route, content: null);

            response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        }

        [Theory]
        [InlineData("/api/books")]
        [InlineData("/api/borrowers")]
        public async Task Post_EmptyJsonObject_ReachesTheServiceAndFailsItsRule(string route)
        {
            // The request DTOs use nullable strings on purpose. Under [ApiController] a
            // non-nullable reference property would gain an implicit [Required], MVC would answer
            // 400 itself, and the service's own validation would never run - so the error message
            // would stop naming the field the way the rest of the API does. This is the test that
            // notices if somebody "tidies" the `string?` away.
            _fixture.Lending
                .AddBookAsync(Arg.Any<Proto.AddBookRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.Book>(StatusCode.InvalidArgument, "'title' is required."));
            _fixture.Lending
                .AddBorrowerAsync(Arg.Any<Proto.AddBorrowerRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.Borrower>(StatusCode.InvalidArgument, "'fullName' is required."));

            var response = await _fixture.CreateClient().PostAsync(route, Json("{}"));

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            // The service's words, not the framework's: proof the call was actually made.
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
            // `?from=&to=` binds to null for a nullable target, so the report runs over all time.
            // The minimal-API binder called TryParse("") and answered 400 instead. Empty-as-absent
            // is the more conventional reading of a query string, and it is now a decision.
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
            // `limit` binds fine - it is an int - so the cap and the sign are the service's rule to
            // enforce, in one place, rather than being half-checked at the edge as well.
            _fixture.Analytics
                .GetMostBorrowedBooksAsync(Arg.Any<Proto.MostBorrowedBooksRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(ApiHostFixture.Fails<Proto.MostBorrowedBooksResponse>(
                    StatusCode.InvalidArgument, "'limit' must not be negative."));

            var response = await _fixture.CreateClient().GetAsync("/api/books/most-borrowed?limit=-5");

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await Detail(response)).ShouldContain("negative");
        }

        [Theory]
        [InlineData("/api/borrowers/3/reading-pace")]
        [InlineData("/api/books/2/also-borrowed")]
        public async Task InvertedWindow_IsRejectedOnEveryRouteThatTakesOne(string route)
        {
            // The two routes the existing inverted-window tests did not cover.
            _fixture.Analytics.ClearReceivedCalls();

            var response = await _fixture.CreateClient()
                .GetAsync($"{route}?from=2026-04-01&to=2026-01-01");

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
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
