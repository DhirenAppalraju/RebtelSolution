using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Proto = Library.Contracts.V1;

namespace Library.Api
{
    /// <summary>API host; WebApplicationFactory entry-point marker.</summary>
    public sealed class ApiHost
    {
        private ApiHost()
        {
        }

        public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddOptions<LibraryClientOptions>()
                .Bind(builder.Configuration.GetSection(LibraryClientOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.AddControllers()
                .AddApplicationPart(typeof(ApiHost).Assembly);

            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GrpcExceptionHandler>();
            builder.Services.AddOpenApi(options => options.AddOperationTransformer((operation, context, _) =>
            {
                // Top borrowers: window is required.
                if (context.Description.RelativePath == "api/borrowers/top" && operation.Parameters != null)
                {
                    foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>()
                                 .Where(p => p.Name == "from" || p.Name == "to"))
                    {
                        parameter.Required = true;
                    }
                }

                return Task.CompletedTask;
            }));

            // Before registrations below, so test overrides win.
            if (configure != null)
            {
                configure(builder);
            }

            builder.Services.TryAddSingleton(TimeProvider.System);
            builder.Services.TryAddSingleton<DeadlineInterceptor>();

            var endpoint = new Uri(
                builder.Configuration[$"{LibraryClientOptions.SectionName}:GrpcEndpoint"] ?? "http://localhost:5210");

            builder.Services.AddGrpcClient<Proto.LendingService.LendingServiceClient>(o => o.Address = endpoint)
                .AddInterceptor<DeadlineInterceptor>();

            builder.Services.AddGrpcClient<Proto.AnalyticsService.AnalyticsServiceClient>(o => o.Address = endpoint)
                .AddInterceptor<DeadlineInterceptor>();

            var app = builder.Build();

            app.UseExceptionHandler();

            // Unmatched routes get a problem document.
            app.UseStatusCodePages();

            // Docs in every environment: security out of scope.
            app.MapOpenApi();
            app.MapScalarApiReference("/docs", options => options.WithTitle("Library API"));

            app.MapControllers();

            return app;
        }
    }
}
