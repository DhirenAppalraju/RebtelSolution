using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Library.Service.Persistence
{
    /// <summary>
    /// Instants stored as UTC DateTime: EF cannot range-compare DateTimeOffset on SQLite,
    /// and every report filters on a date.
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
