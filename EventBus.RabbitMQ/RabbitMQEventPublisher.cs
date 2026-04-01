using EventBus.Events;

namespace EventBus.RabbitMQ;

/// <summary>
/// Implements IEventPublisher by publishing integration events to a RabbitMQ topic exchange.
/// The exchange name is the "topic" equivalent in Kafka terms.
/// Routing key = event type FullName (mirrors Kafka message key).
/// Message value = JSON-serialized MessageEnvelop (same wire format as Kafka).
/// </summary>
public class RabbitMQEventPublisher(
    string exchangeName,
    IChannel channel,
    ILogger logger) : IEventPublisher
{
    // RabbitMQ channels are NOT thread-safe — guard concurrent publishes with a semaphore.
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<bool> PublishAsync<TEvent>(TEvent @event) where TEvent : IntegrationEvent
    {
        var json = JsonSerializer.Serialize(@event, @event.GetType());
        var routingKey = @event.GetType().FullName!;

        logger.LogInformation("Publishing event {type} to exchange {exchange}: {event}",
            @event.GetType().Name, exchangeName, json);

        try
        {
            var envelop = new MessageEnvelop(typeof(TEvent), json);
            var body = JsonSerializer.SerializeToUtf8Bytes(envelop);

            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = @event.EventId.ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await _semaphore.WaitAsync();
            try
            {
                await channel.BasicPublishAsync(
                    exchange: exchangeName,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: props,
                    body: body);
            }
            finally
            {
                _semaphore.Release();
            }

            logger.LogInformation("Published event {eventId}", @event.EventId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing event {eventId}", @event.EventId);
            return false;
        }
    }
}
