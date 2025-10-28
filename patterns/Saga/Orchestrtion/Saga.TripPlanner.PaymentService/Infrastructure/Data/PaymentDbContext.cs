using Microsoft.EntityFrameworkCore;
using Saga.TripPlanner.PaymentService.Infrastructure.Entity;

namespace Saga.TripPlanner.PaymentService.Infrastructure.Data;
public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<CreditCard> CreditCards { get; set; } = default!;
}
