using Microsoft.EntityFrameworkCore;
using Saga.TripPlanner.TicketService.Infrastructure.Data;

namespace Saga.TripPlanner.TicketService.Bootstraping
{
    public static class ApplicationServiceExtensions
    {
        public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOpenApi();

            // Add EF Core
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<TicketDbContext>(options => options.UseNpgsql(connectionString));

            return builder;
        }

    }
}
