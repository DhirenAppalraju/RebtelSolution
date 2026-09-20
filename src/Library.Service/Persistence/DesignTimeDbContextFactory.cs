using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Library.Service.Persistence
{
    /// <summary>Lets `dotnet ef` build the model without starting the host.</summary>
    public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>
    {
        public LibraryDbContext CreateDbContext(string[] args)
        {
            return new LibraryDbContext(
                new DbContextOptionsBuilder<LibraryDbContext>().UseSqlite("Data Source=library.db").Options);
        }
    }
}
