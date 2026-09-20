using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Service.Persistence.Configurations
{
    public sealed class BookConfiguration : IEntityTypeConfiguration<Book>
    {
        public void Configure(EntityTypeBuilder<Book> builder)
        {
            builder.ToTable("Books");
            builder.HasKey(b => b.Id);

            builder.Property(b => b.Title).IsRequired().HasMaxLength(200);
            builder.Property(b => b.Author).IsRequired().HasMaxLength(200);
            builder.Property(b => b.PageCount).IsRequired();

            builder.HasMany(b => b.Copies)
                   .WithOne(c => c.Book!)
                   .HasForeignKey(c => c.BookId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(b => b.Copies).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(b => b.Title);
        }
    }
}
