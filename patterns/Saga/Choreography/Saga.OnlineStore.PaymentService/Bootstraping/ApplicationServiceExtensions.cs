using EventBus;
using EventBus.Kafka;
using Microsoft.EntityFrameworkCore;
using Saga.OnlineStore.IntegrationEvents;
using Saga.OnlineStore.PaymentService.Infrastructure.Data;


namespace Saga.OnlineStore.PaymentService.Bootstraping;
public static class ApplicationServiceExtensions
{
    public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
        // Add EF Core
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<PaymentDbContext>(options => options.UseNpgsql(connectionString));


        builder.Services.ConfigureKafkaProducer(builder.Configuration);
        var kafkaTopic = "Saga-OnlineStore-PaymentService";
        builder.AddKafkaEventPublisher(kafkaTopic);

        var eventConsumingTopics = "Saga-OnlineStore-InventoryService";
        if (!string.IsNullOrEmpty(eventConsumingTopics))
        {
            builder.AddKafkaEventConsumer(options =>
            {
                options.ServiceName = "OnlineStorePaymentService";
                options.KafkaGroupId = "saga-onlinestore-payment-service";
                options.Topics.AddRange(eventConsumingTopics.Split(','));
                options.IntegrationEventFactory = IntegrationEventFactory<ProductCreatedIntegrationEvent>.Instance;
                options.AcceptEvent = e => e.IsEvent<OrderItemsReservedIntegrationEvent>();
            });
        }

        return builder;
    }
}
