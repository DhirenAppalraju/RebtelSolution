namespace Library.Service.Persistence
{
    public sealed class DatabaseOptions
    {
        public const string SectionName = "Database";

        /// <summary>Migrate and seed at start-up: no setup needed.</summary>
        public bool MigrateOnStartup { get; set; } = true;

        public bool SeedDemoData { get; set; } = true;
    }
}
