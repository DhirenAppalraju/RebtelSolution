namespace Library.Service.Persistence
{
    public sealed class DatabaseOptions
    {
        public const string SectionName = "Database";

        /// <summary>Apply migrations (and seed) at start-up, so the demo needs no setup.</summary>
        public bool MigrateOnStartup { get; set; } = true;

        public bool SeedDemoData { get; set; } = true;
    }
}
