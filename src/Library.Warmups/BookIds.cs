namespace Library.Warmups
{
    public static class BookIds
    {
        // Bit trick; > 0 guard excludes 0 and int.MinValue. BCL equivalent: int.IsPow2.
        public static bool IsPowerOfTwo(int id)
        {
            return id > 0 && (id & (id - 1)) == 0;
        }

        // != 0, not == 1: == 1 fails for negatives. Range inclusive; both ends are even anyway.
        public static IEnumerable<int> OddIds(int maxInclusive = 100)
        {
            for (var id = 1; id <= maxInclusive; id += 2)
            {
                yield return id;
            }
        }

        // Printing split from generating: keeps the generator testable.
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
