using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Library.FunctionalTests.Api
{
    /// <summary>
    /// Keeps the README's route table and the code from drifting. The fixture runs in
    /// Production, so the document is proven not to be Development-only.
    /// </summary>
    public class OpenApiDocumentTests : IClassFixture<ApiHostFixture>
    {
        private readonly ApiHostFixture _fixture;

        public OpenApiDocumentTests(ApiHostFixture fixture)
        {
            _fixture = fixture;
        }

        private static readonly string[] TransportProblems = new string[] { "400", "503", "504" };

        [Fact]
        public async Task Document_IsServedInEveryEnvironment()
        {
            var response = await _fixture.CreateClient().GetAsync("/openapi/v1.json");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Docs_AreServedInEveryEnvironment()
        {
            var response = await _fixture.CreateClient().GetAsync("/docs");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task EveryApiRoute_DeclaresTheTransportProblems()
        {
            var paths = await Paths();

            foreach (var path in paths.EnumerateObject().Where(p => p.Name.StartsWith("/api", StringComparison.Ordinal)))
            {
                foreach (var operation in path.Value.EnumerateObject())
                {
                    var responses = operation.Value.GetProperty("responses");

                    foreach (var status in TransportProblems)
                    {
                        responses.TryGetProperty(status, out _)
                            .ShouldBeTrue($"{operation.Name.ToUpperInvariant()} {path.Name} should declare {status}");
                    }
                }
            }
        }

        [Theory]
        [InlineData("/api/books/{id}", "get", "404")]
        [InlineData("/api/borrowers/{id}", "get", "404")]
        [InlineData("/api/loans/{id}", "get", "404")]
        [InlineData("/api/borrowers/{id}/reading-pace", "get", "404")]
        [InlineData("/api/books/{id}/also-borrowed", "get", "404")]
        [InlineData("/api/borrowers", "post", "409")]
        [InlineData("/api/loans", "post", "404")]
        [InlineData("/api/loans", "post", "409")]
        [InlineData("/api/loans/{id}/return", "post", "409")]
        public async Task RouteSpecificFailures_AreDeclaredWhereTheTableSaysTheyAre(string path, string verb, string status)
        {
            var responses = (await Paths()).GetProperty(path).GetProperty(verb).GetProperty("responses");

            responses.TryGetProperty(status, out _).ShouldBeTrue($"{verb.ToUpperInvariant()} {path} should declare {status}");
        }

        [Fact]
        public async Task TopBorrowers_MarksTheWindowRequired()
        {
            var parameters = (await Paths())
                .GetProperty("/api/borrowers/top").GetProperty("get").GetProperty("parameters");

            foreach (var name in new string[] { "from", "to" })
            {
                var parameter = parameters.EnumerateArray()
                    .Single(p => p.GetProperty("name").GetString() == name);

                parameter.GetProperty("required").GetBoolean().ShouldBeTrue($"'{name}' should be required");
            }
        }

        [Fact]
        public async Task OptionalWindows_StayOptional()
        {
            var parameters = (await Paths())
                .GetProperty("/api/books/most-borrowed").GetProperty("get").GetProperty("parameters");

            var from = parameters.EnumerateArray().Single(p => p.GetProperty("name").GetString() == "from");

            from.TryGetProperty("required", out var required).ShouldBeFalse_Or(required);
        }

        private async Task<JsonElement> Paths()
        {
            var document = await _fixture.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json");
            return document.GetProperty("paths");
        }
    }

    internal static class OptionalAssertions
    {
        /// <summary>OpenAPI may omit `required` or emit false; both mean optional.</summary>
        public static void ShouldBeFalse_Or(this bool present, JsonElement required)
        {
            if (present)
            {
                required.GetBoolean().ShouldBeFalse();
            }
        }
    }
}
