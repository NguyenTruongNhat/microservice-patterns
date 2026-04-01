using Microsoft.EntityFrameworkCore;
using Saga.TripPlanner.HotelService.Infrastructure.Data;

namespace Saga.TripPlanner.HotelService.Bootstraping
{
    public static class ApplicationServiceExtensions
    {
        public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOpenApi();

            // Add EF Core
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<HotelDbContext>(options => options.UseNpgsql(connectionString));

            return builder;
        }

    }
}
