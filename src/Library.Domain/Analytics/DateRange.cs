namespace Library.Domain.Analytics
{
    /// <summary>Half-open [From, To). An absent bound is unbounded.</summary>
    public readonly struct DateRange : IEquatable<DateRange>
    {
        public DateRange(DateTimeOffset? from, DateTimeOffset? to)
        {
            if (from.HasValue && to.HasValue && from.Value >= to.Value)
            {
                throw new Exceptions.ValidationException($"'from' ({from:O}) must be earlier than 'to' ({to:O}).");
            }

            From = from;
            To = to;
        }

        public static DateRange All
        {
            get { return new DateRange(null, null); }
        }

        public DateTimeOffset? From { get; }

        public DateTimeOffset? To { get; }

        public bool IsBounded
        {
            get { return From.HasValue && To.HasValue; }
        }

        public static bool operator ==(DateRange left, DateRange right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DateRange left, DateRange right)
        {
            return !left.Equals(right);
        }

        // Hand-written: equal windows must compare equal.
        public bool Equals(DateRange other)
        {
            return From.Equals(other.From) && To.Equals(other.To);
        }

        public override bool Equals(object? obj)
        {
            return obj is DateRange other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(From, To);
        }

        public override string ToString()
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "[{0}, {1})",
                From.HasValue ? From.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture) : "-",
                To.HasValue ? To.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture) : "-");
        }
    }
}
