using Library.Warmups;

namespace Library.Warmups.Tests
{
    public class BookIdsTests
    {
        [Theory]
        [InlineData(1, true)]      // 2^0
        [InlineData(2, true)]
        [InlineData(4, true)]
        [InlineData(1 << 30, true)]
        [InlineData(0, false)]
        [InlineData(3, false)]
        [InlineData(-8, false)]
        [InlineData(int.MinValue, false)]
        [InlineData(int.MaxValue, false)]
        public void IsPowerOfTwo_Boundaries_MatchExpectation(int id, bool expected)
        {
            BookIds.IsPowerOfTwo(id).ShouldBe(expected);
        }

        [Fact]
        public void IsPowerOfTwo_AcrossRange_AgreesWithBcl()
        {
            for (var id = -1024; id <= 1024; id++)
            {
                BookIds.IsPowerOfTwo(id).ShouldBe(int.IsPow2(id), $"id {id}");
            }
        }

        [Fact]
        public void OddIds_DefaultRange_HasFiftyAscendingOddValues()
        {
            var ids = BookIds.OddIds().ToArray();

            ids.Length.ShouldBe(50);
            ids[0].ShouldBe(1);
            ids[ids.Length - 1].ShouldBe(99);
            ids.ShouldAllBe(id => id % 2 != 0);
            ids.ShouldBe(ids.Order().ToArray());
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        public void OddIds_SmallMaximums_YieldExpectedCount(int maxInclusive, int expectedCount)
        {
            BookIds.OddIds(maxInclusive).Count().ShouldBe(expectedCount);
        }

        [Fact]
        public void PrintOddBookIds_WritesOneIdPerLine()
        {
            var writer = new StringWriter();

            BookIds.PrintOddBookIds(writer);

            var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            lines.Length.ShouldBe(50);
            lines[0].ShouldBe("1");
            lines[lines.Length - 1].ShouldBe("99");
        }
    }
}
