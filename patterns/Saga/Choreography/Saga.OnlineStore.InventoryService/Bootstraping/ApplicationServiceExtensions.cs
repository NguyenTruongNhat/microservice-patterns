using EventBus;
using EventBus.Kafka;
using Microsoft.EntityFrameworkCore;
using Saga.OnlineStore.IntegrationEvents;
using Saga.OnlineStore.InventoryService.APIs;
using Saga.OnlineStore.InventoryService.Infrastructure.Data;
using KafkaConsumerInitializationService;
using Confluent.Kafka.Admin;



namespace Saga.OnlineStore.InventoryService.Bootstraping
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
            builder.Services.AddDbContext<InventoryDbContext>(options => options.UseNpgsql(connectionString));

            builder.Services.ConfigureKafkaProducer(builder.Configuration);

            var kafkaTopic = "Saga-OnlineStore-InventoryService";

            // need to improve
            var kafkaConnectionString = builder.Configuration.GetValue<string>("KafkaConnection");
            builder.KafkaTopicInitializer(options =>
            {
                options.BootstrapServers = kafkaConnectionString!;
                options.Topics = new List<TopicSpecification>
            {
                new TopicSpecification { Name = "Saga-OnlineStore-CatalogService", NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = "Saga-OnlineStore-OrderService", NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = "Saga-OnlineStore-PaymentService", NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = "Saga-OnlineStore-InventoryService", NumPartitions = 1, ReplicationFactor = 1 },
            };
            });

            builder.AddKafkaEventPublisher(kafkaTopic);

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
