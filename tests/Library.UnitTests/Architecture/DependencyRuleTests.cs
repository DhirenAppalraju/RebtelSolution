using System.Reflection;
using Library.Api;
using Library.Domain.Entities;
using Library.Service;

namespace Library.UnitTests.Architecture
{
    /// <summary>
    /// The README's two structural claims: the API cannot reach the domain or the database,
    /// and the domain depends on nothing. One ProjectReference from quietly breaking, and no
    /// other test would notice.
    /// </summary>
    /// <remarks>
    /// Read off the compiled manifest, not the csproj: asserts no type is used, not just
    /// that the reference is absent.
    /// </remarks>
    public class DependencyRuleTests
    {
        [Theory]
        [InlineData("Library.Domain")]
        [InlineData("Library.Service")]
        public void LibraryApi_CannotReachTheDomainOrTheDatabase(string forbidden)
        {
            References(typeof(ApiHost).Assembly)
                .ShouldNotContain(forbidden, "Library.Api owns HTTP only; everything else is the service's.");
        }

        [Fact]
        public void LibraryApi_TalksToTheServiceThroughTheContractsOnly()
        {
            References(typeof(ApiHost).Assembly)
                .Where(name => name.StartsWith("Library.", StringComparison.Ordinal))
                .ShouldBe(new string[] { "Library.Contracts" });
        }

        [Theory]
        [InlineData("Microsoft.EntityFrameworkCore")]
        [InlineData("Microsoft.AspNetCore")]
        [InlineData("Grpc")]
        [InlineData("Google.Protobuf")]
        [InlineData("Library.")]
        public void LibraryDomain_DependsOnNothing(string forbiddenPrefix)
        {
            References(typeof(Book).Assembly)
                .ShouldNotContain(
                    name => name.StartsWith(forbiddenPrefix, StringComparison.Ordinal),
                    $"the domain must stay free of {forbiddenPrefix}: its csproj is empty on purpose.");
        }

        [Fact]
        public void LibraryService_IsTheOnlyProjectThatOwnsPersistence()
        {
            // Mirror of the rule above: the database sits on one side of the wire.
            References(typeof(ServiceHost).Assembly)
                .ShouldContain(name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        }

        private static IReadOnlyList<string> References(Assembly assembly)
        {
            return assembly.GetReferencedAssemblies()
                .Select(name => name.Name ?? string.Empty)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }
    }
}
