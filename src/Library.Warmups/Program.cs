namespace Library.Warmups
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Warm-up 1: is a Book ID a power of two?");
            foreach (var id in new int[] { 1, 2, 3, 16, 100, 1024 })
            {
                Console.WriteLine($"  {id,5} -> {BookIds.IsPowerOfTwo(id)}");
            }

            Console.WriteLine();
            Console.WriteLine("Warm-up 2: reverse a title");
            Console.WriteLine($"  \"Moby Dick\" -> \"{BookTitles.ReverseTitle("Moby Dick")}\"");

            Console.WriteLine();
            Console.WriteLine("Warm-up 3: repeat a title");
            Console.WriteLine($"  (\"Read\", 3) -> \"{BookTitles.RepeatTitle("Read", 3)}\"");

            Console.WriteLine();
            Console.WriteLine("Warm-up 4: odd Book IDs between 0 and 100");
            BookIds.PrintOddBookIds(Console.Out);
        }
    }
}
