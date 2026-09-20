using Library.Domain.Analytics;
using Library.Domain.Exceptions;

namespace Library.UnitTests.Domain
{
    public class DateRangeTests
    {
        private static DateTimeOffset Day(int day)
        {
            return new DateTimeOffset(2026, 1, day, 0, 0, 0, TimeSpan.Zero);
        }

        [Fact]
        public void Ctor_InvertedRange_Throws()
        {
            Should.Throw<ValidationException>(() => new DateRange(Day(5), Day(2)));
        }

        [Fact]
        public void Ctor_EqualBounds_Throws()
        {
            Should.Throw<ValidationException>(() => new DateRange(Day(5), Day(5)));
        }

        [Fact]
        public void Equality_ComparesBothBounds()
        {
            // Equality is hand-written, not record-generated, so it needs testing like any code.
            var january = new DateRange(Day(1), Day(31));

            january.Equals(new DateRange(Day(1), Day(31))).ShouldBeTrue();
            (january == new DateRange(Day(1), Day(31))).ShouldBeTrue();
            (january != new DateRange(Day(1), Day(31))).ShouldBeFalse();

            (january == new DateRange(Day(2), Day(31))).ShouldBeFalse();
            (january == new DateRange(Day(1), Day(30))).ShouldBeFalse();
            (january != new DateRange(Day(1), Day(30))).ShouldBeTrue();
        }

        [Fact]
        public void Equality_TreatsAbsentBoundsAsEqual()
        {
            DateRange.All.ShouldBe(new DateRange(null, null));
            (DateRange.All == default).ShouldBeTrue();
            (DateRange.All == new DateRange(Day(1), null)).ShouldBeFalse();
            (new DateRange(null, Day(1)) == new DateRange(Day(1), null)).ShouldBeFalse();
        }

        [Fact]
        public void Equality_AgreesWithGetHashCode()
        {
            var a = new DateRange(Day(1), Day(31));
            var b = new DateRange(Day(1), Day(31));

            a.GetHashCode().ShouldBe(b.GetHashCode());

            // And the object overload, used by dictionaries and Shouldly.
            a.Equals((object)b).ShouldBeTrue();
            a.Equals("not a range").ShouldBeFalse();
            a.Equals(null).ShouldBeFalse();
        }

        [Fact]
        public void ToString_ShowsTheHalfOpenShape()
        {
            DateRange.All.ToString().ShouldBe("[-, -)");
            new DateRange(Day(1), null).ToString().ShouldStartWith("[2026-01-01");
            new DateRange(Day(1), Day(31)).ToString().ShouldEndWith(")");
        }

        [Fact]
        public void IsBounded_OnlyWhenBothEndsArePresent()
        {
            new DateRange(Day(2), null).IsBounded.ShouldBeFalse();
            new DateRange(null, Day(5)).IsBounded.ShouldBeFalse();
            new DateRange(Day(2), Day(5)).IsBounded.ShouldBeTrue();
        }
    }
}
