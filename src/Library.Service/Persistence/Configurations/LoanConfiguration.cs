using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Service.Persistence.Configurations
{
    public sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
    {
        public const string OpenLoanPerCopyIndex = "UX_Loans_OpenLoanPerCopy";
        public const string OpenTitlePerBorrowerIndex = "UX_Loans_OpenTitlePerBorrower";

        public void Configure(EntityTypeBuilder<Loan> builder)
        {
            builder.ToTable("Loans");
            builder.HasKey(l => l.Id);

            builder.HasOne(l => l.BookCopy).WithMany().HasForeignKey(l => l.BookCopyId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(l => l.Book).WithMany().HasForeignKey(l => l.BookId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(l => l.Borrower).WithMany().HasForeignKey(l => l.BorrowerId).OnDelete(DeleteBehavior.Restrict);

            // One open loan per copy: closes the last-copy race in the schema.
            builder.HasIndex(l => l.BookCopyId)
                   .IsUnique()
                   .HasFilter("\"ReturnedAt\" IS NULL")
                   .HasDatabaseName(OpenLoanPerCopyIndex);

            // No two copies of one title per member.
            builder.HasIndex(l => new { l.BorrowerId, l.BookId })
                   .IsUnique()
                   .HasFilter("\"ReturnedAt\" IS NULL")
                   .HasDatabaseName(OpenTitlePerBorrowerIndex);

            // Returned once: UPDATE ... WHERE ReturnedAt IS NULL.
            builder.Property(l => l.ReturnedAt).IsConcurrencyToken();

            // Report access paths: grouped column first, date last.
            builder.HasIndex(l => new { l.BookId, l.BorrowedAt });
            builder.HasIndex(l => new { l.BorrowerId, l.BorrowedAt });
            builder.HasIndex(l => l.BorrowedAt);
        }
    }
}
