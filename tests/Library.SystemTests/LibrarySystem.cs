using Library.Api;
using Library.Service;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Library.SystemTests
{
    /// <summary>
    /// Both hosts started for real on loopback sockets. This is the only tier that exercises h2c
    /// between two Kestrel hosts, which no in-memory transport can.
    /// </summary>
    public sealed class LibrarySystem : IAsyncLifetime
    {
        private readonly string _database = Path.Combine(Path.GetTempPath(), $"library-system-{Guid.NewGuid():N}.db");
        private WebApplication? _service;
        private WebApplication? _api;

        public FakeTimeProvider Clock { get; } =
            new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));

        public HttpClient Client { get; private set; } = null!;

        /// <summary>The service's own h2c address, so a test can reach it without going through the API.</summary>
        public string ServiceAddress { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            _service = ServiceHost.Build(
                new string[]
                {
                    "--urls", "http://127.0.0.1:0",
                    "--ConnectionStrings:Library", $"Data Source={_database}",
                },
                // Build runs this before its own registrations, so the fake clock wins the TryAdd.
                builder => builder.Services.TryAddSingleton<TimeProvider>(Clock));

            await _service.StartAsync();

            // No protocol override is needed here: the service sets HTTP/2 in code.
            var grpcEndpoint = Address(_service);
            ServiceAddress = grpcEndpoint;

            // The API keeps the real clock: its only use of one is the gRPC deadline, and a frozen
            // clock would put every deadline in the past. Lending timestamps come from the service.
            _api = ApiHost.Build(
                new string[] { "--urls", "http://127.0.0.1:0", "--Library:GrpcEndpoint", grpcEndpoint });

            await _api.StartAsync();

            Client = new HttpClient { BaseAddress = new Uri(Address(_api)) };
        }

        public async Task DisposeAsync()
        {
            Client?.Dispose();

            if (_api != null)
            {
                await _api.StopAsync();
                await _api.DisposeAsync();
            }

            if (_service != null)
            {
                await _service.StopAsync();
                await _service.DisposeAsync();
            }

            SqliteConnection.ClearAllPools();
            File.Delete(_database);
        }

        private static string Address(WebApplication app)
        {
            return app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!
                .Addresses.First();
        }
    }

    [CollectionDefinition(nameof(LibrarySystemCollection))]
    public sealed class LibrarySystemCollection : ICollectionFixture<LibrarySystem>
    {
    }

    // A second, independent pair of hosts and database. Journeys that write get their own world, so
    // the fixture assertions above can stay exact.
    [CollectionDefinition(nameof(LibraryWriteCollection))]
    public sealed class LibraryWriteCollection : ICollectionFixture<LibrarySystem>
    {
    }
}
