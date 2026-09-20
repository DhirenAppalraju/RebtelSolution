using Library.Domain.Entities;
using Library.Domain.Exceptions;

namespace Library.UnitTests.Domain
{
    public class BorrowerTests
    {
        public static TheoryData<string, string, string> Invalid =>
            new TheoryData<string, string, string>
            {
                { "", "a@example.com", "fullName" },
                { "   ", "a@example.com", "fullName" },
                { new string('n', 201), "a@example.com", "fullName" },
                { "A Member", "", "email" },
                { "A Member", "   ", "email" },
                { "A Member", "not-an-address", "email" },
                { "A Member", "@example.com", "email" },
                { "A Member", "two@@example.com", "email" },
                { "A Member", new string('e', 250) + "@example.com", "email" },
            };

        [Theory]
        [MemberData(nameof(Invalid))]
        public void Create_InvalidInput_ThrowsNamingTheField(string fullName, string email, string field)
        {
            Should.Throw<ValidationException>(() => Borrower.Create(fullName, email))
                .Message.ShouldContain(field);
        }

        [Fact]
        public void Create_DisplayNameForm_StoresOnlyTheAddress()
        {
            // MailAddress accepts "Name <addr>". Storing the input verbatim would put the whole
            // string in the Email column, where the unique index cannot see that the address
            // inside it already belongs to somebody.
            var borrower = Borrower.Create("Impostor", "Ava Chen <ava.chen@example.com>");

            borrower.Email.ShouldBe("ava.chen@example.com");
        }

        [Theory]
        [InlineData("Ava.Chen@Example.COM")]
        [InlineData("AVA.CHEN@EXAMPLE.COM")]
        [InlineData("  ava.chen@example.com  ")]
        public void Create_NormalisesTheAddress_SoTheUniqueIndexIsARealRule(string email)
        {
            // RFC 5321 makes the local part case-sensitive; no provider treats it that way, and
            // two members differing only by case is a support ticket rather than a feature.
            Borrower.Create("A Member", email).Email.ShouldBe("ava.chen@example.com");
        }

        [Fact]
        public void Create_TrimsTheName()
        {
            Borrower.Create("  Ava Chen  ", "ava.chen@example.com").FullName.ShouldBe("Ava Chen");
        }

        [Theory]
        [InlineData("a@b.co")]
        [InlineData("first.last+tag@sub.example.com")]
        public void Create_WellFormedAddresses_AreAccepted(string email)
        {
            Borrower.Create("A Member", email).Email.ShouldBe(email);
        }

        [Theory]
        // Display-name form: the name is stripped and only the address is kept.
        [InlineData("Ava Chen <ava.chen@example.com>", "ava.chen@example.com")]
        [InlineData("spaces in@example.com", "in@example.com")]
        // Legal per RFC 5322 even though they look wrong: no TLD, trailing dot, quoted local part.
        [InlineData("a@b", "a@b")]
        [InlineData("trailing@example.com.", "trailing@example.com.")]
        [InlineData("\"quoted local\"@example.com", "\"quoted local\"@example.com")]
        public void Create_TheBclParserIsPermissive_AndWeStoreWhatItParsed(string input, string stored)
        {
            // Pinning the parser's real behaviour rather than a stricter one we might assume.
            // MailAddress validates *shape*, not deliverability, and it accepts more than most
            // people expect. That is tolerable precisely because the parsed address is what gets
            // stored, so the unique index still sees one canonical value per member. Proving an
            // address reaches a human is a confirmation email's job, not a regex's.
            Borrower.Create("A Member", input).Email.ShouldBe(stored);
        }
    }
}
