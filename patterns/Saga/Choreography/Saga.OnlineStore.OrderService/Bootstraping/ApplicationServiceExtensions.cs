using EventBus;
using EventBus.Kafka;
using Microsoft.EntityFrameworkCore;
using Saga.OnlineStore.IntegrationEvents;
using Saga.OnlineStore.OrderService.Infrastructure.Data;


namespace Saga.OnlineStore.OrderService.Bootstraping
{
    public static class ApplicationServiceExtensions
    {
        public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOpenApi();

            builder.Services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
            });

            // Add EF Core
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<OrderDbContext>(options => options.UseNpgsql(connectionString));

            builder.Services.ConfigureKafkaProducer(builder.Configuration);

            // You can also get it from configuration
            var kafkaTopic = "Saga-OnlineStore-OrderService";
            builder.AddKafkaEventPublisher(kafkaTopic);

            var eventConsumingTopics = "Saga-OnlineStore-InventoryService,Saga-OnlineStore-PaymentService";
            if (!string.IsNullOrEmpty(eventConsumingTopics))
            {
                builder.AddKafkaEventConsumer(options =>
                {
                    options.ServiceName = "OrderService";
                    options.KafkaGroupId = "saga-order-service";
                    options.Topics.AddRange(eventConsumingTopics.Split(','));
                    options.IntegrationEventFactory = IntegrationEventFactory<ProductCreatedIntegrationEvent>.Instance;
                    options.AcceptEvent = e => e.IsEvent<OrderItemsReservationFailedIntegrationEvent, OrderPaymentApprovedIntegrationEvent, OrderPaymentRejectedIntegrationEvent>();
                });
            }

            return builder;
        }
    }
}
