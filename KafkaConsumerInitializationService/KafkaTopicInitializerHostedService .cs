namespace KafkaConsumerInitializationService
{
    using Confluent.Kafka.Admin;
    using Confluent.Kafka;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Linq;

    public class KafkaTopicInitializerHostedService : IHostedService
    {
        private readonly ILogger<KafkaTopicInitializerHostedService> _logger;
        private readonly KafkaTopicInitializerOptions _options;

        public KafkaTopicInitializerHostedService(
            ILogger<KafkaTopicInitializerHostedService> logger,
            KafkaTopicInitializerOptions options)
        {
            _logger = logger;
            _options = options;
        }


        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Kafka topic initialization...");

            _logger.LogInformation("Checking existing Kafka topics...");

            var config = new AdminClientConfig { BootstrapServers = _options.BootstrapServers };
            using var adminClient = new AdminClientBuilder(config).Build();

            try
            {
                // Get existing topics metadata
                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
                var existingTopics = metadata.Topics.Select(t => t.Topic).ToHashSet();

                // Filter out topics that already exist
                var newTopics = _options.Topics
                    .Where(t => !existingTopics.Contains(t.Name))
                    .ToList();

                if (newTopics.Count == 0)
                {
                    _logger.LogInformation("All Kafka topics already exist. No topics to create.");
                    return;
                }

                _logger.LogInformation("Creating Kafka topics: {Topics}", string.Join(", ", newTopics.Select(t => t.Name)));

                await adminClient.CreateTopicsAsync(newTopics, new CreateTopicsOptions { });

                _logger.LogInformation("Kafka topics created successfully.");
            }
            catch (CreateTopicsException ex)
            {
                _logger.LogError(ex, "Failed to create Kafka topics");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during Kafka topic initialization");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // No-op
            return Task.CompletedTask;
        }
    }

}
