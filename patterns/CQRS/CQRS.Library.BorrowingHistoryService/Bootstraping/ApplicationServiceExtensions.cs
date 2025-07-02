using Confluent.Kafka.Admin;
using CQRS.Library.BorrowingHistoryService.Infrastructure.Data;
using CQRS.Library.IntegrationEvents;
using EventBus;
using EventBus.Kafka;
using KafkaConsumerInitializationService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CQRS.Library.BorrowingHistoryService.Bootstraping;
public static class ApplicationServiceExtensions
{
    public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();

        // Add EF Core
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<BorrowingHistoryDbContext>(options => options.UseNpgsql(connectionString));

        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
        });

        var kafkaConnectionString = builder.Configuration.GetValue<string>("KafkaConnection");     
        builder.KafkaTopicInitializer(options =>
        {
            options.BootstrapServers = kafkaConnectionString!;
            options.Topics = new List<TopicSpecification>
            {
                new TopicSpecification { Name = "BookServiceTP", NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = "BorrowerServiceTP", NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = "BorrowingServiceTP", NumPartitions = 1, ReplicationFactor = 1 },
            };
        });
        
        var eventConsumingTopics = new List<string> {
                                        "BookServiceTP",
                                        "BorrowerServiceTP",
                                        "BorrowingServiceTP"};

        if (eventConsumingTopics.Count > 0)
        {
            builder.AddKafkaEventConsumer(options =>
            {
                options.ServiceName = "BorrowingHistoryService";
                options.KafkaGroupId = "cqrs";
                options.Topics.AddRange(eventConsumingTopics);
                options.IntegrationEventFactory = IntegrationEventFactory<BookCreatedIntegrationEvent>.Instance;
            });
        }

        return builder;
    }
}

