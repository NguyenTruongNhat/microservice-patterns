using CQRS.Library.BorrowingService.Infrastructure.Entity;
using Microsoft.EntityFrameworkCore;

namespace CQRS.Library.BorrowingService.Infrastructure.Data;
public class BorrowingDbContext(DbContextOptions<BorrowingDbContext> options) : DbContext(options)
{
    public DbSet<Borrowing> Borrowings { get; set; } = default!;
}
