using EventBus.Kafka;
using Microsoft.EntityFrameworkCore;
using Saga.OnlineStore.OrderService.Infrastructure.Data;


namespace Saga.OnlineStore.OrderService.Bootstraping
{
    public static class ApplicationServiceExtensions
    {
        public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOpenApi();
            // Add EF Core
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<OrderDbContext>(options => options.UseNpgsql(connectionString));

            builder.Services.ConfigureKafkaProducer(builder.Configuration);
            builder.AddKafkaEventPublisher("Saga-OnlineStore-OrderService");

            return builder;
        }
    }
}
