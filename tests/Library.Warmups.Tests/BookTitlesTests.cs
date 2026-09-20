using Library.Warmups;

namespace Library.Warmups.Tests
{
    public class BookTitlesTests
    {
        [Fact]
        public void ReverseTitle_BriefsExample_IsReversed()
        {
            BookTitles.ReverseTitle("Moby Dick").ShouldBe("kciD yboM");
        }

        [Theory]
        [InlineData("")]
        [InlineData("A")]
        public void ReverseTitle_EmptyOrSingleElement_ReturnsInput(string title)
        {
            BookTitles.ReverseTitle(title).ShouldBe(title);
        }

        [Fact]
        public void ReverseTitle_SurrogatePair_SurvivesIntact()
        {
            BookTitles.ReverseTitle("ab\U0001F4DA").ShouldBe("\U0001F4DAba");
        }

        [Fact]
        public void ReverseTitle_CombiningMark_KeepsAccentOnItsLetter()
        {
            BookTitles.ReverseTitle("café x").ShouldBe("x éfac");
        }

        [Fact]
        public void ReverseTitle_Null_Throws()
        {
            Should.Throw<ArgumentNullException>(() => BookTitles.ReverseTitle(null!));
        }

        [Fact]
        public void RepeatTitle_ThreeTimes_ConcatenatesInOrder()
        {
            BookTitles.RepeatTitle("Read", 3).ShouldBe("ReadReadRead");
        }

        [Theory]
        [InlineData("Read", 0)]
        [InlineData("", 5)]
        public void RepeatTitle_NothingToRepeat_ReturnsEmpty(string title, int times)
        {
            BookTitles.RepeatTitle(title, times).ShouldBe(string.Empty);
        }

        [Fact]
        public void RepeatTitle_NegativeCount_Throws()
        {
            Should.Throw<ArgumentOutOfRangeException>(() => BookTitles.RepeatTitle("Read", -1));
        }

        [Fact]
        public void RepeatTitle_Null_Throws()
        {
            Should.Throw<ArgumentNullException>(() => BookTitles.RepeatTitle(null!, 3));
        }

        [Fact]
        public void RepeatTitle_LengthOverflow_ThrowsOverflowRatherThanExhaustingMemory()
        {
            Should.Throw<OverflowException>(() => BookTitles.RepeatTitle("ab", int.MaxValue));
        }
    }
}
