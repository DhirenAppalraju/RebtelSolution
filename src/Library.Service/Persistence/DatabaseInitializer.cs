using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Library.Service.Persistence
{
    /// <summary>Applies the migration at start-up so the demo needs no setup. Seeding rides along inside Migrate via UseSeeding.</summary>
    public sealed partial class DatabaseInitializer : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly IOptions<DatabaseOptions> _options;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(
            IServiceProvider services,
            IOptions<DatabaseOptions> options,
            ILogger<DatabaseInitializer> logger)
        {
            _services = services;
            _options = options;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_options.Value.MigrateOnStartup)
            {
                SkippingMigration(_logger);
                return;
            }

            using (var scope = _services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();

                await db.Database.MigrateAsync(cancellationToken);
                MigrationApplied(_logger);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Database is up to date.")]
        private static partial void MigrationApplied(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Database:MigrateOnStartup is false; skipping migration.")]
        private static partial void SkippingMigration(ILogger logger);
    }
}
