using Library.Domain.Lending;
using Library.Service.Application;
using Library.Service.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Library.IntegrationTests
{
    /// <summary>
    /// Real SQLite on one in-memory connection, schema built by the migration, so every test
    /// proves the migration applies cleanly.
    /// </summary>
    public sealed class LibraryDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly bool _seeded;

        private LibraryDatabase(bool seeded)
        {
            _seeded = seeded;
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            using (var context = NewContext())
            {
                context.Database.Migrate();
            }
        }

        public FakeTimeProvider Clock { get; } =
            new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));

        public LendingPolicy Policy { get; } = new LendingPolicy();

        public CommandCounter Commands { get; } = new CommandCounter();

        public static LibraryDatabase Seeded()
        {
            return new LibraryDatabase(seeded: true);
        }

        public static LibraryDatabase Empty()
        {
            return new LibraryDatabase(seeded: false);
        }

        /// <summary>
        /// Extra interceptors stage a competing write mid-call - see <see cref="StageTheRace"/>.
        /// </summary>
        public LibraryDbContext NewContext(params IInterceptor[] extra)
        {
            var options = new DbContextOptionsBuilder<LibraryDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(Commands)
                .AddInterceptors(extra);

            if (_seeded)
            {
                options.UseSeeding((db, _) => DemoData.Seed((LibraryDbContext)db))
                       .UseAsyncSeeding((db, _, ct) => DemoData.SeedAsync((LibraryDbContext)db, ct));
            }

            return new LibraryDbContext(options.Options);
        }

        public LendingService NewLendingService(LibraryDbContext context)
        {
            return new LendingService(context, Options.Create(Policy), Clock);
        }

        public AnalyticsService NewAnalyticsService(LibraryDbContext context)
        {
            return new AnalyticsService(context, Options.Create(Policy));
        }

        public void Dispose()
        {
            _connection.Dispose();
            SqliteConnection.ClearAllPools();
        }
    }
}
