namespace Library.Service
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            await ServiceHost.Build(args).RunAsync();
        }
    }
}
