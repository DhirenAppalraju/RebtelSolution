using System.Globalization;

namespace Library.Warmups
{
    public static class BookTitles
    {
        // Reverses text elements so surrogate pairs and combining marks survive; title.Reverse() would not.
        public static string ReverseTitle(string title)
        {
            ArgumentNullException.ThrowIfNull(title);

            if (title.Length <= 1)
            {
                return title;
            }

            var elements = new List<string>(title.Length);
            var enumerator = StringInfo.GetTextElementEnumerator(title);
            while (enumerator.MoveNext())
            {
                elements.Add((string)enumerator.Current);
            }

            elements.Reverse();
            return string.Concat(elements);
        }

        public static string RepeatTitle(string title, int times)
        {
            ArgumentNullException.ThrowIfNull(title);
            ArgumentOutOfRangeException.ThrowIfNegative(times);

            if (times == 0 || title.Length == 0)
            {
                return string.Empty;
            }

            // checked: a huge count fails as an overflow rather than as out-of-memory.
            _ = checked(title.Length * times);

            return string.Concat(Enumerable.Repeat(title, times));
        }
    }
}
