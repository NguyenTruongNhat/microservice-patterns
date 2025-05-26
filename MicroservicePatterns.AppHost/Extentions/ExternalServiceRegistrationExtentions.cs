using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MicroservicePatterns.AppHost.Extentions
{
    public static class ExternalServiceRegistrationExtentions
    {
        public static IDistributedApplicationBuilder AddApplicationServices(this IDistributedApplicationBuilder builder)
        {
            var cache = builder.AddRedis("redis");
            var kafka = builder.AddKafka("kafka");
            var mongoDb = builder.AddMongoDB("mongodb");
            var postgres = builder.AddPostgres("postgres");

            if (!builder.Configuration.GetValue("IsTest", false))
            {
                cache = cache.WithLifetime(ContainerLifetime.Persistent).WithDataVolume().WithRedisInsight();
                kafka = kafka.WithLifetime(ContainerLifetime.Persistent).WithDataVolume().WithKafkaUI();
                mongoDb = mongoDb.WithLifetime(ContainerLifetime.Persistent).WithDataVolume().WithMongoExpress();
                postgres = postgres.WithLifetime(ContainerLifetime.Persistent).WithDataVolume().WithPgWeb();
            }


            return builder;
        }

        private static async Task CreateKafkaTopics(ResourceReadyEvent @event, KafkaServerResource kafkaResource, CancellationToken ct)
        {
            var logger = @event.Services.GetRequiredService<ILogger<Program>>();
        }

    }
}

