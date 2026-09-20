using Library.Api;
using Library.Domain.Lending;
using Library.Service;
using Library.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Library.FunctionalTests.Hosts
{
    /// <summary>
    /// Composition, not logic: a bad setting stops the host, and the shipped settings bind
    /// and validate. Both hosts are built for real, hence the functional tier.
    /// </summary>
    public class HostConfigurationTests
    {
        [Fact]
        public void LendingPolicy_ZeroConcurrentLoans_IsRejectedNamingTheSetting()
        {
            using (var app = ServiceHost.Build(new string[] { "--Lending:MaxConcurrentLoans=0" }))
            {
                var error = Should.Throw<OptionsValidationException>(() =>
                    app.Services.GetRequiredService<IOptions<LendingPolicy>>().Value);

                error.Message.ShouldContain(nameof(LendingPolicy.MaxConcurrentLoans));
            }
        }

        [Fact]
        public void LendingPolicy_DefaultLimitAboveTheCap_IsRejected()
        {
            using (var app = ServiceHost.Build(
                new string[] { "--Lending:DefaultReportLimit=500", "--Lending:MaxReportLimit=100" }))
            {
                var error = Should.Throw<OptionsValidationException>(() =>
                    app.Services.GetRequiredService<IOptions<LendingPolicy>>().Value);

                error.Message.ShouldContain("MaxReportLimit");
            }
        }

        [Fact]
        public void LendingPolicy_WithNoConfiguration_FallsBackToTheCodeDefaults()
        {
            // No appsettings.json in the test project: this reads the property initialisers.
            // The file is checked below.
            using (var app = ServiceHost.Build(new string[0]))
            {
                var policy = app.Services.GetRequiredService<IOptions<LendingPolicy>>().Value;

                policy.LoanPeriodDays.ShouldBe(21);
                policy.MaxConcurrentLoans.ShouldBe(5);
                policy.DefaultReportLimit.ShouldBe(10);
                policy.MaxReportLimit.ShouldBe(100);
            }
        }

        [Fact]
        public void LibraryClientOptions_DeadlineOutOfRange_IsRejected()
        {
            using (var app = ApiHost.Build(new string[] { "--Library:GrpcDeadlineSeconds=0" }))
            {
                Should.Throw<OptionsValidationException>(() =>
                    app.Services.GetRequiredService<IOptions<LibraryClientOptions>>().Value);
            }
        }

        [Fact]
        public void LibraryClientOptions_WithNoConfiguration_FallsBackToTheServicePort()
        {
            using (var app = ApiHost.Build(new string[0]))
            {
                var options = app.Services.GetRequiredService<IOptions<LibraryClientOptions>>().Value;

                options.GrpcEndpoint.ShouldBe("http://localhost:5210");
                options.GrpcDeadlineSeconds.ShouldBe(10);
            }
        }

        [Fact]
        public async Task BadSetting_StopsTheHostOnStart_NotOnTheFirstRequestThatNeedsIt()
        {
            // The tests above validate lazily and would pass with ValidateOnStart() deleted.
            // This one starts the host, which is what the registration is for.
            using (var app = ServiceHost.Build(new string[] { "--Lending:MaxConcurrentLoans=0" }))
            {
                var error = await Should.ThrowAsync<OptionsValidationException>(() => app.StartAsync());

                error.Message.ShouldContain(nameof(LendingPolicy.MaxConcurrentLoans));
            }
        }

        [Fact]
        public void ShippedServiceSettings_BindAndPassValidation()
        {
            // The only test that reads the shipped appsettings.json; every other host runs from the
            // test content root. A typo there would otherwise surface only at deployment.
            using (var app = ServiceHost.Build(FromContentRoot("src/Library.Service")))
            {
                // Only in the file: the code's fallback is a `??`, so reading it back proves the load.
                app.Configuration["ConnectionStrings:Library"].ShouldBe("Data Source=library.db");

                var policy = app.Services.GetRequiredService<IOptions<LendingPolicy>>().Value;
                policy.DefaultReportLimit.ShouldBeLessThanOrEqualTo(policy.MaxReportLimit);

                var database = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
                database.MigrateOnStartup.ShouldBeTrue();
                database.SeedDemoData.ShouldBeTrue();
            }
        }

        [Fact]
        public void ShippedApiSettings_BindAndPassValidation()
        {
            using (var app = ApiHost.Build(FromContentRoot("src/Library.Api")))
            {
                app.Configuration[$"{LibraryClientOptions.SectionName}:GrpcEndpoint"].ShouldNotBeNull();

                var options = app.Services.GetRequiredService<IOptions<LibraryClientOptions>>().Value;

                options.GrpcEndpoint.ShouldBe("http://localhost:5210");
                options.GrpcDeadlineSeconds.ShouldBe(10);
            }
        }

        [Fact]
        public async Task MigrateOnStartup_False_LeavesTheDatabaseAlone()
        {
            // The README's deployment turns this off and migrates as a deploy job; nothing proved
            // the switch was wired.
            var file = TemporaryDatabase();

            using (var app = ServiceHost.Build(new string[]
            {
                "--urls", "http://127.0.0.1:0",
                "--Database:MigrateOnStartup=false",
                "--ConnectionStrings:Library", $"Data Source={file}",
            }))
            {
                await app.StartAsync();
                await app.StopAsync();
            }

            // SQLite creates the file on first connection: its absence is the assertion.
            File.Exists(file).ShouldBeFalse("MigrateOnStartup:false must not open the database");
        }

        [Fact]
        public async Task SeedDemoData_False_MigratesButLeavesTheLibraryEmpty()
        {
            var file = TemporaryDatabase();

            try
            {
                using (var app = ServiceHost.Build(new string[]
                {
                    "--urls", "http://127.0.0.1:0",
                    "--Database:SeedDemoData=false",
                    "--ConnectionStrings:Library", $"Data Source={file}",
                }))
                {
                    await app.StartAsync();

                    using (var scope = app.Services.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();

                        // Schema present...
                        (await db.Database.GetAppliedMigrationsAsync()).ShouldNotBeEmpty();

                        // ...fixture absent.
                        (await db.Books.AnyAsync()).ShouldBeFalse();
                        (await db.Loans.AnyAsync()).ShouldBeFalse();
                    }

                    await app.StopAsync();
                }
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                File.Delete(file);
            }
        }

        private static string TemporaryDatabase()
        {
            return Path.Combine(Path.GetTempPath(), $"library-config-{Guid.NewGuid():N}.db");
        }

        /// <summary>Runs a host from a source project's directory, so its appsettings.json is under test.</summary>
        private static string[] FromContentRoot(string projectPath)
        {
            return new string[]
            {
                "--contentRoot", Path.Combine(RepositoryRoot(), projectPath),

                // Pinned, so a set ASPNETCORE_ENVIRONMENT cannot test a different file than CI does.
                "--environment", Environments.Production,
            };
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Library.sln")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                throw new InvalidOperationException(
                    $"Library.sln was not found above {AppContext.BaseDirectory}.");
            }

            return directory.FullName;
        }
    }
}
