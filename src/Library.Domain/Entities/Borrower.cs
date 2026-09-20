using System.Net.Mail;
using Library.Domain.Exceptions;

namespace Library.Domain.Entities
{
    public sealed class Borrower
    {
        private Borrower()
        {
        }

        public int Id { get; private set; }

        public string FullName { get; private set; } = string.Empty;

        public string Email { get; private set; } = string.Empty;

        public static Borrower Create(string fullName, string email)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new ValidationException("'fullName' is required.");
            }

            if (fullName.Length > 200)
            {
                throw new ValidationException("'fullName' must be 200 characters or fewer.");
            }

            MailAddress? address;
            if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !MailAddress.TryCreate(email, out address))
            {
                throw new ValidationException("'email' must be a well-formed address of 256 characters or fewer.");
            }

            // Store the parsed address, not the input. MailAddress accepts the display-name form,
            // so "Ava Chen <ava.chen@example.com>" would otherwise be stored whole and slip past
            // the unique index on an address that already exists.
            //
            // Lowercased so the index is a real uniqueness rule. RFC 5321 makes the local part
            // case-sensitive, but no mail provider treats it that way, and two members differing
            // only by case is a support ticket, not a feature.
            return new Borrower
            {
                FullName = fullName.Trim(),
                Email = address.Address.ToLowerInvariant(),
            };
        }
    }
}
