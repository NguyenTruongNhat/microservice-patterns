using CQRS.Library.BookService.Infrastructure.Entity;
using Microsoft.EntityFrameworkCore;

namespace CQRS.Library.BookService.Infrastructure.Data;


public partial class BookDbContext : DbContext
{
    public BookDbContext(DbContextOptions<BookDbContext> options)
        : base(options)
    {
    }

    public DbSet<Book> Books { get; set; } = default!;

}