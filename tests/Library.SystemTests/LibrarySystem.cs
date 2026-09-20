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
    /// Both hosts on real loopback sockets: the only tier that exercises h2c between two
    /// Kestrel hosts.
    /// </summary>
    public sealed class LibrarySystem : IAsyncLifetime
    {
        private readonly string _database = Path.Combine(Path.GetTempPath(), $"library-system-{Guid.NewGuid():N}.db");
        private WebApplication? _service;
        private WebApplication? _api;

        public FakeTimeProvider Clock { get; } =
            new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));

        public HttpClient Client { get; private set; } = null!;

        /// <summary>The service's h2c address, reachable without the API.</summary>
        public string ServiceAddress { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            _service = ServiceHost.Build(
                new string[]
                {
                    "--urls", "http://127.0.0.1:0",
                    "--ConnectionStrings:Library", $"Data Source={_database}",
                },
                // Runs before Build's registrations, so the fake clock wins the TryAdd.
                builder => builder.Services.TryAddSingleton<TimeProvider>(Clock));

            await _service.StartAsync();

            // No protocol override: the service sets HTTP/2 in code.
            var grpcEndpoint = Address(_service);
            ServiceAddress = grpcEndpoint;

            // API keeps the real clock: it only uses one for the gRPC deadline, and a frozen clock
            // would put every deadline in the past. Timestamps come from the service.
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

    // A second, independent pair of hosts and database: writing journeys get their own
    // world, so the fixture assertions above stay exact.
    [CollectionDefinition(nameof(LibraryWriteCollection))]
    public sealed class LibraryWriteCollection : ICollectionFixture<LibrarySystem>
    {
    }
}
