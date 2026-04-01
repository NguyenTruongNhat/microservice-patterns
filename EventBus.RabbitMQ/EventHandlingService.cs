using EventBus.Events;

namespace EventBus.RabbitMQ;

/// <summary>
/// BackgroundService that consumes messages from a RabbitMQ queue and dispatches them via MediatR.
/// Mirrors EventBus.Kafka's EventHandlingService in structure and responsibility.
///
/// Flow:
///   1. Declare exchange (topic) + queue + binding (wildcard "#")
///   2. AsyncEventingBasicConsumer receives messages
///   3. Deserialize MessageEnvelop → resolve typed IntegrationEvent via IIntegrationEventFactory
///   4. Check AcceptEvent filter
///   5. Dispatch via IMediator.Send() inside a fresh DI scope
///   6. Manual Ack on success / Nack+requeue on failure
/// </summary>
public class EventHandlingService : BackgroundService
{
    private readonly IConnection _connection;
    private readonly EventHandlingWorkerOptions _options;
    private readonly IIntegrationEventFactory _integrationEventFactory;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger _logger;
    private IChannel? _channel;

    public EventHandlingService(
        IConnection connection,
        EventHandlingWorkerOptions options,
        IIntegrationEventFactory integrationEventFactory,
        IServiceScopeFactory serviceScopeFactory,
        ILoggerFactory loggerFactory)
    {
        _connection = connection;
        _options = options;
        _integrationEventFactory = integrationEventFactory;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = loggerFactory.CreateLogger(_options.ServiceName);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Subscribing to exchange [{exchange}], queue [{queue}]...",
            _options.ExchangeName, _options.QueueName);

        // Each consumer owns its own channel — safe, isolated from the publisher channel.
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Mirror Kafka topic → RabbitMQ topic exchange (durable, persists restarts)
        await _channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        // Named queue: services in the same consumer group share one queue (same as Kafka GroupId)
        await _channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        // Wildcard "#" binding = subscribe to all routing keys on this exchange.
        // AcceptEvent filter (same as Kafka) handles fine-grained event selection in-process.
        await _channel.QueueBindAsync(
            queue: _options.QueueName,
            exchange: _options.ExchangeName,
            routingKey: "#",
            cancellationToken: stoppingToken);

        // Prefetch 1 = process one message at a time, only fetch next after Ack.
        // Mirrors Kafka's sequential per-partition processing.
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var envelop = JsonSerializer.Deserialize<MessageEnvelop>(ea.Body.Span)
                    ?? throw new InvalidOperationException("Failed to deserialize MessageEnvelop.");

                // New scope per message: EventHandlingService is Singleton, handlers are typically Scoped.
                using IServiceScope scope = _serviceScopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                await ProcessMessageAsync(mediator, envelop, stoppingToken);

                // Explicit Ack: message removed from queue only after successful processing.
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from queue [{queue}]", _options.QueueName);

                // Nack + requeue: message goes back to queue for retry.
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: _options.QueueName,
            autoAck: false,   // Manual ack — same reliability guarantee as Kafka's manual commit
            consumer: consumer,
            cancellationToken: stoppingToken);

        // Hold the loop alive until the host shuts down.
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private async Task ProcessMessageAsync(IMediator mediator, MessageEnvelop message, CancellationToken cancellationToken)
    {
        var @event = _integrationEventFactory.CreateEvent(message.MessageTypeName, message.Message);

        if (@event is not null)
        {
            if (_options.AcceptEvent(@event))
            {
                _logger.LogInformation("Processing message {t}: {message}", message.MessageTypeName, message.Message);
                await mediator.Send(@event, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Event skipped: {t}", message.MessageTypeName);
            }
        }
        else
        {
            _logger.LogWarning("Event type not found: {t}", message.MessageTypeName);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Configuration options for EventHandlingService.
/// Mirrors EventHandlingWorkerOptions in EventBus.Kafka.
/// </summary>
public class EventHandlingWorkerOptions
{
    /// <summary>RabbitMQ exchange name. Equivalent to Kafka topic group.</summary>
    public string ExchangeName { get; set; } = "integration-events";

    /// <summary>
    /// Queue name. Services sharing the same queue name act as a consumer group (load-balanced).
    /// Each distinct service should use a unique queue name.
    /// Equivalent to Kafka's GroupId + topic subscription.
    /// </summary>
    public string QueueName { get; set; } = "event-handling";

    /// <summary>Factory to resolve IntegrationEvent from MessageEnvelop type name + JSON.</summary>
    public IIntegrationEventFactory IntegrationEventFactory { get; set; } = EventBus.IntegrationEventFactory.Instance;

    /// <summary>Logger category name shown in log output.</summary>
    public string ServiceName { get; set; } = "EventHandlingService";

    /// <summary>
    /// Filter predicate — return false to skip events this service does not handle.
    /// Use IsEvent&lt;T&gt; helpers for clean syntax.
    /// </summary>
    public Func<IntegrationEvent, bool> AcceptEvent { get; set; } = _ => true;
}
