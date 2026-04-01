using Microsoft.EntityFrameworkCore;
using Saga.TripPlanner.PaymentService.Infrastructure.Data;

namespace Saga.TripPlanner.PaymentService.Bootstraping
{
    public static class ApplicationServiceExtensions
    {
        public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOpenApi();

            // Add EF Core
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<PaymentDbContext>(options => options.UseNpgsql(connectionString));

            return builder;
        }

    }
}
