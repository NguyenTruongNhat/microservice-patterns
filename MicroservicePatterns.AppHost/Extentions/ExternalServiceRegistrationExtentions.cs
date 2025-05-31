using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;

namespace MicroservicePatterns.AppHost.Extentions
{
    public static class ExternalServiceRegistrationExtentions
    {
        public static IDistributedApplicationBuilder AddApplicationServices(this IDistributedApplicationBuilder builder)
        {
            var cache = builder.AddRedis("redis");
            var kafka = builder.AddKafka("kafka");
            var mongoDb = builder.AddMongoDB("mongodb");
            var postgres = builder.AddPostgres("postgresql");

            if (!builder.Configuration.GetValue("IsTest", false))
            {
                cache = cache.WithLifetime(ContainerLifetime.Persistent).WithDataVolume().WithRedisInsight();
                kafka = kafka.WithLifetime(ContainerLifetime.Persistent).WithDataVolume().WithKafkaUI();
                mongoDb = mongoDb.WithLifetime(ContainerLifetime.Persistent).WithDataVolume().WithMongoExpress();
                postgres = postgres.WithLifetime(ContainerLifetime.Persistent).WithDataVolume().WithPgWeb();
            }
            builder.Eventing.Subscribe<ResourceReadyEvent>(kafka.Resource, async (@event, ct) =>
            {
                await CreateKafkaTopics(@event, kafka.Resource, ct);
            });

            #region CQRS Library
            var bookDb = postgres.AddDefaultDatabase<Projects.CQRS_Library_BookService>();
            var bookService = builder.AddProjectWithPostfix<Projects.CQRS_Library_BookService>()
                .WithEnvironment(Consts.Env_EventPublishingTopics, GetTopicName<Projects.CQRS_Library_BookService>())
                .WithReference(kafka)
                .WithReference(bookDb, Consts.DefaultDatabase)
                .WaitFor(bookDb)
                .WaitFor(kafka);



            #endregion

            return builder;
        }

        private static async Task CreateKafkaTopics(ResourceReadyEvent @event, KafkaServerResource kafkaResource, CancellationToken ct)
        {
            var logger = @event.Services.GetRequiredService<ILogger<Program>>();

            TopicSpecification[] topics = [
                new() { Name = GetTopicName<Projects.CQRS_Library_BookService>(), NumPartitions = 1, ReplicationFactor = 1 },
            new() { Name = GetTopicName<Projects.CQRS_Library_BorrowerService>(), NumPartitions = 1, ReplicationFactor = 1 },
            new() { Name = GetTopicName<Projects.CQRS_Library_BorrowingService>(), NumPartitions = 1, ReplicationFactor = 1 },
            ];

            logger.LogInformation("Creating topics: {topics} ...", string.Join(", ", topics.Select(t => t.Name).ToArray()));

            var connectionString = await kafkaResource.ConnectionStringExpression.GetValueAsync(ct);
            using var adminClient = new AdminClientBuilder(new AdminClientConfig()
            {
                BootstrapServers = connectionString,
            }).Build();
            try
            {
                await adminClient.CreateTopicsAsync(topics, new CreateTopicsOptions() { });
            }
            catch (CreateTopicsException ex)
            {
                logger.LogError(ex, "An error occurred creating topics");
            }
        }
        private static string GetTopicName<TProject>(string postfix = "") => $"{typeof(TProject).Name.Replace('_', '-')}{(string.IsNullOrEmpty(postfix) ? "" : $"-{postfix}")}";
    }
    public class Consts
    {
        public const string DefaultDatabase = "DefaultDatabase";
        public const string Env_EventPublishingTopics = "EVENT_PUBLISHING_TOPICS";
        public const string Env_EventConsumingTopics = "EVENT_CONSUMING_TOPICS";
    }
}

