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

            // Store the parsed address: MailAddress accepts "Name <a@b>", which would slip past the unique index.
            // Lowercased so that index is a real uniqueness rule.
            return new Borrower
            {
                FullName = fullName.Trim(),
                Email = address.Address.ToLowerInvariant(),
            };
        }
    }
}
