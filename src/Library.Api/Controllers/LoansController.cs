using Library.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Proto = Library.Contracts.V1;

namespace Library.Api.Controllers
{
    [ApiController]
    [Route("api/loans")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public class LoansController : LibraryControllerBase
    {
        private readonly Proto.LendingService.LendingServiceClient _lending;

        public LoansController(Proto.LendingService.LendingServiceClient lending)
        {
            _lending = lending;
        }

        /// <summary>Borrow a book.</summary>
        [HttpPost("", Name = "BorrowBook")]
        [EndpointSummary("Borrow a book")]
        [EndpointDescription("Lends the lowest-numbered free copy. 409 when every copy is out, the borrower is at the loan limit, or they already hold this title.")]
        [ProducesResponseType(typeof(LoanResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> BorrowBook(
            [FromBody] BorrowBookRequest request, CancellationToken cancellationToken)
        {
            var loan = await _lending.BorrowBookAsync(
                new Proto.BorrowBookRequest { BorrowerId = request.BorrowerId, BookId = request.BookId },
                cancellationToken: cancellationToken);

            var created = ApiMapping.ToResponse(loan);
            return Created($"/api/loans/{created.Id}", created);
        }

        /// <summary>One loan by id.</summary>
        [HttpGet("{id:int}", Name = "GetLoan")]
        [ProducesResponseType(typeof(LoanResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLoan(int id, CancellationToken cancellationToken)
        {
            var loan = await _lending.GetLoanAsync(
                new Proto.GetLoanRequest { LoanId = id }, cancellationToken: cancellationToken);

            return Ok(ApiMapping.ToResponse(loan));
        }

        // POST, not PATCH: an action with rules, not a field set.

        /// <summary>Return a book.</summary>
        [HttpPost("{id:int}/return", Name = "ReturnBook")]
        [EndpointSummary("Return a book")]
        [ProducesResponseType(typeof(LoanResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ReturnBook(int id, CancellationToken cancellationToken)
        {
            var loan = await _lending.ReturnBookAsync(
                new Proto.ReturnBookRequest { LoanId = id }, cancellationToken: cancellationToken);

            return Ok(ApiMapping.ToResponse(loan));
        }
    }
}
