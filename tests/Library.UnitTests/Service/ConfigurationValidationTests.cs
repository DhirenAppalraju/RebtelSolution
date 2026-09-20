using Library.Api;
using Library.Domain.Lending;
using Library.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Library.UnitTests.Service
{
    /// <summary>A bad setting should stop the host, not surface hours later on a user's request.</summary>
    public class ConfigurationValidationTests
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
        public void LendingPolicy_Defaults_AreTheDocumentedOnes()
        {
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
        public void LibraryClientOptions_Defaults_PointAtTheServicePort()
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
            // The tests above read IOptions<T>.Value, which validates lazily - so they would all
            // still pass with ValidateOnStart() deleted, and a bad setting would then surface on
            // whichever user request happened to touch it first. This is the one that starts the
            // host, which is the behaviour the registration is actually there for.
            using (var app = ServiceHost.Build(new string[] { "--Lending:MaxConcurrentLoans=0" }))
            {
                var error = await Should.ThrowAsync<OptionsValidationException>(() => app.StartAsync());

                error.Message.ShouldContain(nameof(LendingPolicy.MaxConcurrentLoans));
            }
        }
    }
}
