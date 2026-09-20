using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Library.Service.Persistence
{
    /// <summary>
    /// Stores instants as UTC DateTime. EF will not translate range comparisons on DateTimeOffset for
    /// SQLite and every report filters on a date, so the conversion is solution-wide.
    /// </summary>
    public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTime>
    {
        public UtcDateTimeOffsetConverter()
            : base(
                offset => offset.UtcDateTime,
                utc => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)))
        {
        }
    }
}
