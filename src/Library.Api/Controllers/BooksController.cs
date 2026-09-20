using Library.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Proto = Library.Contracts.V1;

namespace Library.Api.Controllers
{
    [ApiController]
    [Route("api/books")]
    // Controller-wide: any route can get bad input, fail to reach the service, or time out.
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public class BooksController : LibraryControllerBase
    {
        private readonly Proto.LendingService.LendingServiceClient _lending;
        private readonly Proto.AnalyticsService.AnalyticsServiceClient _analytics;

        public BooksController(
            Proto.LendingService.LendingServiceClient lending,
            Proto.AnalyticsService.AnalyticsServiceClient analytics)
        {
            _lending = lending;
            _analytics = analytics;
        }

        /// <summary>The catalogue with availability.</summary>
        [HttpGet("", Name = "ListBooks")]
        [EndpointSummary("The catalogue with availability")]
        [EndpointDescription("Every title with its total and available copies. Availability is derived from open loans, not from the clock.")]
        [ProducesResponseType(typeof(BookListResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListBooks(CancellationToken cancellationToken)
        {
            var response = await _lending.ListBooksAsync(
                new Proto.ListBooksRequest(), cancellationToken: cancellationToken);

            return Ok(ApiMapping.ToResponse(response));
        }

        /// <summary>Add a title and its copies.</summary>
        [HttpPost("", Name = "AddBook")]
        [EndpointSummary("Add a title and its copies")]
        [ProducesResponseType(typeof(BookResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> AddBook(
            [FromBody] AddBookRequest request, CancellationToken cancellationToken)
        {
            var book = await _lending.AddBookAsync(
                new Proto.AddBookRequest
                {
                    Title = request.Title ?? string.Empty,
                    Author = request.Author ?? string.Empty,
                    PageCount = request.PageCount,
                    Copies = request.Copies,
                },
                cancellationToken: cancellationToken);

            var created = ApiMapping.ToResponse(book);
            return Created($"/api/books/{created.Id}", created);
        }

        // Before "{id:int}" for readability; the int constraint does the disambiguating.

        /// <summary>Q1: most borrowed books.</summary>
        [HttpGet("most-borrowed", Name = "GetMostBorrowedBooks")]
        [EndpointSummary("Q1: most borrowed books")]
        [EndpointDescription("Ranked by loan count, then distinct borrowers, then id. " + DateRangeBinding.Format)]
        [ProducesResponseType(typeof(MostBorrowedBooksResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMostBorrowedBooks(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] int? limit,
            CancellationToken cancellationToken)
        {
            var problem = InvalidWindow(from, to, required: false);
            if (problem != null)
            {
                return problem;
            }

            var request = new Proto.MostBorrowedBooksRequest { Limit = limit ?? 0 };

            var period = DateRangeBinding.ToProto(from, to);
            if (period != null)
            {
                request.Period = period;
            }

            var response = await _analytics.GetMostBorrowedBooksAsync(
                request, cancellationToken: cancellationToken);

            return Ok(ApiMapping.ToResponse(response));
        }

        /// <summary>One title by id.</summary>
        [HttpGet("{id:int}", Name = "GetBook")]
        [ProducesResponseType(typeof(BookResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBook(int id, CancellationToken cancellationToken)
        {
            var book = await _lending.GetBookAsync(
                new Proto.GetBookRequest { BookId = id }, cancellationToken: cancellationToken);

            return Ok(ApiMapping.ToResponse(book));
        }

        /// <summary>Q4: what else the borrowers of this book borrowed.</summary>
        [HttpGet("{id:int}/also-borrowed", Name = "GetAlsoBorrowedBooks")]
        [EndpointSummary("Q4: what else the borrowers of this book borrowed")]
        [EndpointDescription("Ranked by shared borrowers, then loans by the cohort, then id. `cohortSize` says how many people borrowed the source title.")]
        [ProducesResponseType(typeof(AlsoBorrowedResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAlsoBorrowedBooks(
            int id,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] int? limit,
            CancellationToken cancellationToken)
        {
            var problem = InvalidWindow(from, to, required: false);
            if (problem != null)
            {
                return problem;
            }

            var request = new Proto.AlsoBorrowedRequest { BookId = id, Limit = limit ?? 0 };

            var period = DateRangeBinding.ToProto(from, to);
            if (period != null)
            {
                request.Period = period;
            }

            var response = await _analytics.GetAlsoBorrowedBooksAsync(
                request, cancellationToken: cancellationToken);

            return Ok(ApiMapping.ToResponse(response));
        }
    }
}
