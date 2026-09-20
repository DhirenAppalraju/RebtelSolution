using Grpc.Net.Client;
using Library.Service;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Proto = Library.Contracts.V1;

namespace Library.FunctionalTests.Service
{
    /// <summary>
    /// Real service host over the in-memory transport, migrated and seeded at start-up.
    /// Drives a generated gRPC client, so a Q4 defect fails as a Q4 test.
    /// </summary>
    public sealed class ServiceHostFixture : WebApplicationFactory<ServiceHost>
    {
        private readonly string _database = Path.Combine(Path.GetTempPath(), $"library-func-{Guid.NewGuid():N}.db");

        public FakeTimeProvider Clock { get; } =
            new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));

        public Proto.LendingService.LendingServiceClient Lending
        {
            get
            {
                return new Proto.LendingService.LendingServiceClient(CreateChannel());
            }
        }

        public Proto.AnalyticsService.AnalyticsServiceClient Analytics
        {
            get
            {
                return new Proto.AnalyticsService.AnalyticsServiceClient(CreateChannel());
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Library", $"Data Source={_database}");

            // Runs after the host's registrations, so Replace, not TryAdd.
            builder.ConfigureTestServices(services =>
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock)));
        }

        public GrpcChannel CreateChannel()
        {
            return GrpcChannel.ForAddress(
                Server.BaseAddress,
                new GrpcChannelOptions
                {
                    HttpHandler = new ResponseVersionHandler { InnerHandler = Server.CreateHandler() },
                });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            SqliteConnection.ClearAllPools();
            File.Delete(_database);
        }

        // TestServer answers HTTP/1.1 by default; the gRPC client needs matching versions.
        private sealed class ResponseVersionHandler : DelegatingHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = await base.SendAsync(request, cancellationToken);
                response.Version = request.Version;
                return response;
            }
        }
    }
}
