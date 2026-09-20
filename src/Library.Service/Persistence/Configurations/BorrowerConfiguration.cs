using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Service.Persistence.Configurations
{
    public sealed class BorrowerConfiguration : IEntityTypeConfiguration<Borrower>
    {
        public void Configure(EntityTypeBuilder<Borrower> builder)
        {
            builder.ToTable("Borrowers");
            builder.HasKey(b => b.Id);

            builder.Property(b => b.FullName).IsRequired().HasMaxLength(200);
            builder.Property(b => b.Email).IsRequired().HasMaxLength(256);

            builder.HasIndex(b => b.Email).IsUnique().HasDatabaseName("UX_Borrowers_Email");
        }
    }
}
