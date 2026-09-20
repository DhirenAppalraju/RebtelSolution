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
            // MailAddress accepts "Name <addr>"; storing it verbatim would hide a duplicate
            // address from the unique index.
            var borrower = Borrower.Create("Impostor", "Ava Chen <ava.chen@example.com>");

            borrower.Email.ShouldBe("ava.chen@example.com");
        }

        [Theory]
        [InlineData("Ava.Chen@Example.COM")]
        [InlineData("AVA.CHEN@EXAMPLE.COM")]
        [InlineData("  ava.chen@example.com  ")]
        public void Create_NormalisesTheAddress_SoTheUniqueIndexIsARealRule(string email)
        {
            // Local part is case-sensitive per RFC 5321, but no provider treats it so.
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
        // Display-name form: name stripped, address kept.
        [InlineData("Ava Chen <ava.chen@example.com>", "ava.chen@example.com")]
        [InlineData("spaces in@example.com", "in@example.com")]
        // Legal per RFC 5322: no TLD, trailing dot, quoted local part.
        [InlineData("a@b", "a@b")]
        [InlineData("trailing@example.com.", "trailing@example.com.")]
        [InlineData("\"quoted local\"@example.com", "\"quoted local\"@example.com")]
        public void Create_TheBclParserIsPermissive_AndWeStoreWhatItParsed(string input, string stored)
        {
            // Pins MailAddress's real behaviour: it validates shape, not deliverability.
            // Tolerable because the parsed address is stored, so the index sees one canonical
            // value per member. Reachability is a confirmation email's job.
            Borrower.Create("A Member", input).Email.ShouldBe(stored);
        }
    }
}
