using Library.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Proto = Library.Contracts.V1;

namespace Library.Api.Controllers
{
    [ApiController]
    [Route("api/borrowers")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public class BorrowersController : LibraryControllerBase
    {
        private readonly Proto.LendingService.LendingServiceClient _lending;
        private readonly Proto.AnalyticsService.AnalyticsServiceClient _analytics;

        public BorrowersController(
            Proto.LendingService.LendingServiceClient lending,
            Proto.AnalyticsService.AnalyticsServiceClient analytics)
        {
            _lending = lending;
            _analytics = analytics;
        }

        /// <summary>Add a borrower.</summary>
        [HttpPost("", Name = "AddBorrower")]
        [EndpointSummary("Add a borrower")]
        [ProducesResponseType(typeof(BorrowerResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> AddBorrower(
            [FromBody] AddBorrowerRequest request, CancellationToken cancellationToken)
        {
            var borrower = await _lending.AddBorrowerAsync(
                new Proto.AddBorrowerRequest
                {
                    FullName = request.FullName ?? string.Empty,
                    Email = request.Email ?? string.Empty,
                },
                cancellationToken: cancellationToken);

            var created = ApiMapping.ToResponse(borrower);
            return Created($"/api/borrowers/{created.Id}", created);
        }

        /// <summary>Q2: top borrowers in a time frame.</summary>
        [HttpGet("top", Name = "GetTopBorrowers")]
        [EndpointSummary("Q2: top borrowers in a time frame")]
        [EndpointDescription("Ranked by loan count, then distinct titles, then id. `from` and `to` are required. " + DateRangeBinding.Format)]
        [ProducesResponseType(typeof(TopBorrowersResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTopBorrowers(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] int? limit,
            CancellationToken cancellationToken)
        {
            // The only report whose window is required, so it is the only one rejected here.
            var problem = InvalidWindow(from, to, required: true);
            if (problem != null)
            {
                return problem;
            }

            var request = new Proto.TopBorrowersRequest
            {
                Limit = limit ?? 0,
                Period = DateRangeBinding.ToProto(from, to),
            };

            var response = await _analytics.GetTopBorrowersAsync(
                request, cancellationToken: cancellationToken);

            return Ok(ApiMapping.ToResponse(response));
        }

        /// <summary>One borrower by id.</summary>
        [HttpGet("{id:int}", Name = "GetBorrower")]
        [ProducesResponseType(typeof(BorrowerResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBorrower(int id, CancellationToken cancellationToken)
        {
            var borrower = await _lending.GetBorrowerAsync(
                new Proto.GetBorrowerRequest { BorrowerId = id }, cancellationToken: cancellationToken);

            return Ok(ApiMapping.ToResponse(borrower));
        }

        /// <summary>Q3: reading pace, pages per day.</summary>
        [HttpGet("{id:int}/reading-pace", Name = "GetReadingPace")]
        [EndpointSummary("Q3: reading pace, pages per day")]
        [EndpointDescription(
            "Sum of pages over sum of whole days held, across completed loans, assuming continuous reading of each book. "
            + "`pagesPerDay` is null when there is nothing to estimate from.")]
        [ProducesResponseType(typeof(ReadingPaceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetReadingPace(
            int id,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            var problem = InvalidWindow(from, to, required: false);
            if (problem != null)
            {
                return problem;
            }

            var request = new Proto.ReadingPaceRequest { BorrowerId = id };

            var period = DateRangeBinding.ToProto(from, to);
            if (period != null)
            {
                request.Period = period;
            }

            var response = await _analytics.GetReadingPaceAsync(
                request, cancellationToken: cancellationToken);

            return Ok(ApiMapping.ToResponse(response));
        }
    }
}
