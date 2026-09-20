using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers
{
    /// <summary>
    /// Controller base: shared window validation, so one problem document shape.
    /// </summary>
    public abstract class LibraryControllerBase : ControllerBase
    {
        /// <summary>400 problem result for an invalid window, else null.</summary>
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
