using EventBus.Kafka;
using Microsoft.EntityFrameworkCore;
using Saga.OnlineStore.InventoryService.APIs;
using Saga.OnlineStore.InventoryService.Infrastructure.Data;

namespace Saga.OnlineStore.InventoryService.Bootstraping
{
    

    public static class ApplicationServiceExtensions
    {
        public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOpenApi();
            // Add EF Core
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<InventoryDbContext>(options => options.UseNpgsql(connectionString));

            builder.Services.ConfigureKafkaProducer(builder.Configuration);
            builder.AddKafkaEventPublisher("Saga-OnlineStore-InventoryService");

            return builder;
        }

    }
}
