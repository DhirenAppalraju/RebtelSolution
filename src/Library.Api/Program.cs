namespace Library.Api
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            await ApiHost.Build(args).RunAsync();
        }
    }
}
