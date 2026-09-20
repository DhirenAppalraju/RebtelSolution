namespace Library.Warmups
{
    public static class BookIds
    {
        // Bit trick; the > 0 guard matters: 0 and int.MinValue would otherwise pass (int.IsPow2 is the BCL equivalent).
        public static bool IsPowerOfTwo(int id)
        {
            return id > 0 && (id & (id - 1)) == 0;
        }

        // n % 2 != 0, not == 1, which fails for negatives; "between 0 and 100" read inclusively, moot since both ends are even.
        public static IEnumerable<int> OddIds(int maxInclusive = 100)
        {
            for (var id = 1; id <= maxInclusive; id += 2)
            {
                yield return id;
            }
        }

        // Printing is separate from generating, so the generator stays testable.
        public static void PrintOddBookIds(TextWriter writer, int maxInclusive = 100)
        {
            ArgumentNullException.ThrowIfNull(writer);

            foreach (var id in OddIds(maxInclusive))
            {
                writer.WriteLine(id);
            }
        }
    }
}
