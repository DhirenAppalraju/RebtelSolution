using Google.Protobuf.WellKnownTypes;
using Library.Contracts.V1;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api
{
    /// <summary>
    /// The API validates only what only it can see: the shape of the window. Whether a window is
    /// required, and what a limit may be, is the service's rule and is enforced once, there.
    /// </summary>
    public static class DateRangeBinding
    {
        public const string Format = "`from` and `to` are ISO dates (`yyyy-MM-dd`); the window is half-open, `[from, to)`.";

        /// <summary>Null when both bounds are absent, so the field is left unset and the service reads it as all time.</summary>
        public static DateRange? ToProto(DateOnly? from, DateOnly? to)
        {
            if (!from.HasValue && !to.HasValue)
            {
                return null;
            }

            var range = new DateRange();

            if (from.HasValue)
            {
                range.From = Timestamp.FromDateTimeOffset(AtUtcMidnight(from.Value));
            }

            if (to.HasValue)
            {
                range.To = Timestamp.FromDateTimeOffset(AtUtcMidnight(to.Value));
            }

            return range;
        }

        /// <summary>Returns a problem when the window cannot be valid, so no gRPC call is made.</summary>
        public static ProblemDetails? Invalid(DateOnly? from, DateOnly? to, bool required)
        {
            if (required && (!from.HasValue || !to.HasValue))
            {
                return new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Missing time window",
                    Detail = $"`from` and `to` are required. {Format}",
                };
            }

            if (from.HasValue && to.HasValue && from.Value >= to.Value)
            {
                return new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Inverted time window",
                    Detail = $"`from` ({from.Value:yyyy-MM-dd}) must be earlier than `to` ({to.Value:yyyy-MM-dd}). {Format}",
                };
            }

            return null;
        }

        private static DateTimeOffset AtUtcMidnight(DateOnly date)
        {
            return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }
    }
}
