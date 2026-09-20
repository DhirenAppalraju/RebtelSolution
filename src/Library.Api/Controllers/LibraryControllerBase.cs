using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers
{
    /// <summary>
    /// Base for the three API controllers. It holds the one piece of validation the API owns, so the
    /// same malformed window produces the same problem document on every route that takes one.
    /// </summary>
    public abstract class LibraryControllerBase : ControllerBase
    {
        /// <summary>Returns a 400 problem result when the window cannot be valid, otherwise null.</summary>
        protected IActionResult? InvalidWindow(DateOnly? from, DateOnly? to, bool required)
        {
            var problem = DateRangeBinding.Invalid(from, to, required);
            if (problem == null)
            {
                return null;
            }

            return Problem(detail: problem.Detail, statusCode: problem.Status, title: problem.Title);
        }
    }
}
