using Confluent.Kafka.Admin;

namespace KafkaConsumerInitializationService
{
    public class KafkaTopicInitializerOptions
    {
        public string BootstrapServers { get; set; } = string.Empty;
        public List<TopicSpecification> Topics { get; set; } = new List<TopicSpecification>();
    }
}
  