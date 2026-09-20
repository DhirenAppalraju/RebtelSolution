using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Service.Persistence
{
    public sealed class LibraryDbContext : DbContext
    {
        public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
        {
        }

        public DbSet<Book> Books
        {
            get { return Set<Book>(); }
        }

        public DbSet<BookCopy> BookCopies
        {
            get { return Set<BookCopy>(); }
        }

        public DbSet<Borrower> Borrowers
        {
            get { return Set<Borrower>(); }
        }

        public DbSet<Loan> Loans
        {
            get { return Set<Loan>(); }
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder builder)
        {
            builder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ApplyConfigurationsFromAssembly(typeof(LibraryDbContext).Assembly);
        }
    }
}
