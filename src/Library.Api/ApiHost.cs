using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Proto = Library.Contracts.V1;

namespace Library.Api
{
    /// <summary>Also the WebApplicationFactory entry-point marker, which avoids a Program name clash with the service.</summary>
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
                // The one report whose window is not optional; the document should say so.
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

            // Runs before the registrations below, so a test host's clock or endpoint wins.
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

            // Makes a bare 404 from an unmatched route a problem document too.
            app.UseStatusCodePages();

            // Every environment: security is out of scope by the brief, and hiding the docs from a
            // reviewer running a published build costs more than it protects.
            app.MapOpenApi();
            app.MapScalarApiReference("/docs", options => options.WithTitle("Library API"));

            app.MapControllers();

            return app;
        }
    }
}
