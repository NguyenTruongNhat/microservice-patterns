using Microsoft.EntityFrameworkCore;
using Saga.TripPlanner.TripPlanningService.Infrastructure.Data;

namespace Saga.TripPlanner.TripPlanningService.Bootstraping
{
    public static class ApplicationServiceExtensions
    {
        public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOpenApi();

            // Add EF Core
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<TripPlanningDbContext>(options => options.UseNpgsql(connectionString));

            builder.AddSagaClientServices();

            return builder;
        }
    }


    public static class SagaClientServiceExtensions
    {
        public static IHostApplicationBuilder AddSagaClientServices(this IHostApplicationBuilder builder)
        {
            IConfiguration configuration = builder.Configuration;
            builder.Services.AddHttpClient("hotel", client =>
            {
                string url = configuration["ServiceDiscovery:HotelServiceUrl"]
                             ?? throw new InvalidOperationException("HotelService URL not configured.");
                client.BaseAddress = new Uri(url);
            });

            builder.Services.AddHttpClient("ticket", client =>
            {
                string url = configuration["ServiceDiscovery:TicketServiceUrl"]
                             ?? throw new InvalidOperationException("TicketService URL not configured.");
                client.BaseAddress = new Uri(url);
            });

            builder.Services.AddHttpClient("payment", client =>
            {
                string url = configuration["ServiceDiscovery:PaymentServiceUrl"]
                             ?? throw new InvalidOperationException("PaymentService URL not configured.");
                client.BaseAddress = new Uri(url);
            });

            builder.Services.AddScoped<SagaServices>(services =>
            {
                IHttpClientFactory httpClientFactory = services.GetRequiredService<IHttpClientFactory>();

                var s = new SagaServices(
                    httpClientFactory.CreateClient("hotel"),
                    httpClientFactory.CreateClient("ticket"),
                    httpClientFactory.CreateClient("payment")
                );

                return s;
            });

            return builder;
        }
    }
}
