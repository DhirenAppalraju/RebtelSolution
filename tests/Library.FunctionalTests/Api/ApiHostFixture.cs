using Grpc.Core;
using Library.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Proto = Library.Contracts.V1;

namespace Library.FunctionalTests.Api
{
    /// <summary>
    /// The HTTP contract with the service stubbed, which makes downstream failures trivial to provoke.
    /// Runs in Production so the OpenAPI assertions also prove the docs are not Development-only.
    /// </summary>
    public sealed class ApiHostFixture : WebApplicationFactory<ApiHost>
    {
        public const string GrpcEndpoint = "http://localhost:65210";

        // The generated clients are virtual by design, so they substitute cleanly.
        public Proto.LendingService.LendingServiceClient Lending { get; } =
            Substitute.For<Proto.LendingService.LendingServiceClient>();

        public Proto.AnalyticsService.AnalyticsServiceClient Analytics { get; } =
            Substitute.For<Proto.AnalyticsService.AnalyticsServiceClient>();

        /// <summary>A completed call, for stubbing a success.</summary>
        public static AsyncUnaryCall<T> Returns<T>(T value)
        {
            return new AsyncUnaryCall<T>(
                Task.FromResult(value),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }

        /// <summary>A call that fails with a gRPC status, for stubbing every row of the error table.</summary>
        public static AsyncUnaryCall<T> Fails<T>(StatusCode code, string detail = "the service said no")
        {
            var status = new Status(code, detail);
            var error = new RpcException(status);

            return new AsyncUnaryCall<T>(
                Task.FromException<T>(error),
                Task.FromException<Metadata>(error),
                () => status,
                () => new Metadata(),
                () => { });
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting($"{LibraryClientOptions.SectionName}:GrpcEndpoint", GrpcEndpoint);

            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton(Lending));
                services.Replace(ServiceDescriptor.Singleton(Analytics));
            });
        }
    }
}
