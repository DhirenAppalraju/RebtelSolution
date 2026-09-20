using Library.Domain.Lending;
using Library.Service.Application;
using Library.Service.Grpc;
using Library.Service.Persistence;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Library.Service
{
    /// <summary>Service host; WebApplicationFactory entry-point marker.</summary>
    public sealed class ServiceHost
    {
        private ServiceHost()
        {
        }

        public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
        {
            var builder = WebApplication.CreateBuilder(args);

            // In code, not appsettings: survives another content root, and plaintext HTTP/2 needs Http2-only.
            builder.WebHost.ConfigureKestrel(k => k.ConfigureEndpointDefaults(e => e.Protocols = HttpProtocols.Http2));

            builder.Services.AddOptions<LendingPolicy>()
                .Bind(builder.Configuration.GetSection(LendingPolicy.SectionName))
                .ValidateDataAnnotations()
                .Validate(p => p.DefaultReportLimit <= p.MaxReportLimit,
                    "Lending:DefaultReportLimit must not exceed Lending:MaxReportLimit.")
                .ValidateOnStart();

            builder.Services.AddOptions<DatabaseOptions>()
                .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Before registrations below, so a test clock wins.
            configure?.Invoke(builder);

            // TryAdd: a fake clock registered first survives.
            builder.Services.TryAddSingleton(TimeProvider.System);

            var database = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
            var connectionString = builder.Configuration.GetConnectionString("Library") ?? "Data Source=library.db";

            builder.Services.AddDbContext<LibraryDbContext>(options =>
            {
                options.UseSqlite(connectionString);

                if (database.SeedDemoData)
                {
                    // Both overloads: EF tooling calls the synchronous one.
                    options.UseSeeding((db, _) => DemoData.Seed((LibraryDbContext)db))
                           .UseAsyncSeeding((db, _, ct) => DemoData.SeedAsync((LibraryDbContext)db, ct));
                }
            });

            builder.Services.AddScoped<ILendingService, LendingService>();
            builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

            builder.Services.AddGrpc(options => options.Interceptors.Add<DomainExceptionInterceptor>());

            // A registered check is required: with none, the service answers UNKNOWN, and
            // grpc_health_probe reads anything but SERVING as a failure.
            builder.Services.AddGrpcHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy("The service is running."));
            builder.Services.AddHostedService<DatabaseInitializer>();

            var app = builder.Build();

            app.MapGrpcService<LendingGrpcService>();
            app.MapGrpcService<AnalyticsGrpcService>();
            app.MapGrpcHealthChecksService();

            return app;
        }
    }
}
