using EventBus;
using EventBus.Kafka;
using Microsoft.EntityFrameworkCore;
using Saga.OnlineStore.IntegrationEvents;
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

            //You can also get it from configuration
            var eventConsumingTopics = "Saga-OnlineStore-CatalogService,Saga-OnlineStore-OrderService,Saga-OnlineStore-PaymentService"; 

            if (!string.IsNullOrEmpty(eventConsumingTopics))
            {
                builder.AddKafkaEventConsumer(options => {
                    options.ServiceName = "InventoryService";
                    options.KafkaGroupId = "saga-inventory-service";
                    options.Topics.AddRange(eventConsumingTopics.Split(','));
                    options.IntegrationEventFactory = IntegrationEventFactory<ProductCreatedIntegrationEvent>.Instance;
                    options.AcceptEvent = e => e.IsEvent<ProductCreatedIntegrationEvent, OrderPlacedIntegrationEvent, OrderPaymentRejectedIntegrationEvent>();
                });
            }

            return builder;
        }

    }
}
