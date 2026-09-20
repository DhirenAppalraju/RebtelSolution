using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Service.Persistence.Configurations
{
    public sealed class BookCopyConfiguration : IEntityTypeConfiguration<BookCopy>
    {
        public void Configure(EntityTypeBuilder<BookCopy> builder)
        {
            builder.ToTable("BookCopies");
            builder.HasKey(c => c.Id);
            builder.HasIndex(c => c.BookId);
        }
    }
}
