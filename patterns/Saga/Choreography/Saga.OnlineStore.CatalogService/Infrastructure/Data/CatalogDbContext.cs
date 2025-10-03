using Microsoft.EntityFrameworkCore;
using Saga.OnlineStore.CatalogService.Infrastructure.Entity;

namespace Saga.OnlineStore.CatalogService.Infrastructure.Data
{
    public partial class CatalogDbContext : DbContext
    {
        public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
            : base(options)
        {
        }
        public DbSet<Product> Products { get; set; } = default!;

    }
}
