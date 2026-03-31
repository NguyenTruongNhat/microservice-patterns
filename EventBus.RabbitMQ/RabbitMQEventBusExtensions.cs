using EventBus.Events;
using Microsoft.Extensions.Configuration;

namespace EventBus.RabbitMQ;

public static class RabbitMQEventBusExtensions
{
    /// <summary>
    /// Registers a singleton IConnection and a singleton IChannel for publishing.
    /// Call this once in Program.cs before AddRabbitMQEventPublisher.
    /// Mirrors: ConfigureKafkaProducer() in EventBus.Kafka.
    ///
    /// Required appsettings.json:
    /// {
    ///   "RabbitMQ": {
    ///     "HostName": "localhost",
    ///     "Port": 5672,
    ///     "UserName": "guest",
    ///     "Password": "guest",
    ///     "VirtualHost": "/"
    ///   }
    /// }
    /// </summary>
    public static void ConfigureRabbitMQConnection(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("RabbitMQ").Get<RabbitMQSettings>() ?? new RabbitMQSettings();
        services.AddSingleton(settings);

        // IConnection is long-lived and thread-safe — register as Singleton.
        services.AddSingleton<IConnection>(sp =>
        {
            var factory = new ConnectionFactory
            {
                HostName = settings.HostName,
                Port = settings.Port,
                UserName = settings.UserName,
                Password = settings.Password,
                VirtualHost = settings.VirtualHost
            };
            // Sync bootstrap: acceptable in DI registration. Do NOT call inside async path.
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        // Singleton publish channel: shared by all RabbitMQEventPublisher instances.
        // Thread-safety is handled inside RabbitMQEventPublisher via SemaphoreSlim.
        services.AddSingleton<IChannel>(sp =>
        {
            var connection = sp.GetRequiredService<IConnection>();
            return connection.CreateChannelAsync().GetAwaiter().GetResult();
        });
    }

    /// <summary>
    /// Registers IEventPublisher as Transient using the shared publish IChannel.
    /// Call after ConfigureRabbitMQConnection.
    /// Mirrors: AddKafkaEventPublisher() in EventBus.Kafka.
    /// </summary>
    public static void AddRabbitMQEventPublisher(this IHostApplicationBuilder builder, string? exchangeName)
    {
        if (string.IsNullOrWhiteSpace(exchangeName))
            throw new ArgumentNullException(nameof(exchangeName));

        // Declare the exchange at startup so the publisher is ready immediately.
        builder.Services.AddSingleton<IEventPublisher>(services =>
        {
            var channel = services.GetRequiredService<IChannel>();
            channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false).GetAwaiter().GetResult();

            return new RabbitMQEventPublisher(
                exchangeName,
                channel,
                services.GetRequiredService<ILoggerFactory>().CreateLogger($"EventPublisher<{exchangeName}>")
            );
        });
    }

    /// <summary>
    /// Registers the RabbitMQ consumer BackgroundService.
    /// Mirrors: AddKafkaEventConsumer() in EventBus.Kafka.
    ///
    /// Usage:
    ///   builder.AddRabbitMQEventConsumer(options =>
    ///   {
    ///       options.ExchangeName = "integration-events";
    ///       options.QueueName    = "borrowing-service-queue";
    ///       options.IntegrationEventFactory = IntegrationEventFactory&lt;BookCreatedEvent&gt;.Instance;
    ///       options.ServiceName  = "BorrowingService.EventHandler";
    ///       options.AcceptEvent  = e => e.IsEvent&lt;BookCreatedEvent, BorrowerCreatedEvent&gt;();
    ///   });
    /// </summary>
    public static IHostApplicationBuilder AddRabbitMQEventConsumer(
        this IHostApplicationBuilder builder,
        Action<EventHandlingWorkerOptions>? configureOptions = null)
    {
        var options = new EventHandlingWorkerOptions();
        configureOptions?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(_ => options.IntegrationEventFactory);
        builder.Services.AddHostedService<EventHandlingService>();
        return builder;
    }

    // ── IsEvent<T> helpers ──────────────────────────────────────────
    // Convenient filter predicates for AcceptEvent. Mirrors EventBus.Kafka.

    public static bool IsEvent<T1>(this IntegrationEvent @event)
        => @event.GetType() == typeof(T1);

    public static bool IsEvent<T1, T2>(this IntegrationEvent @event)
        => @event.GetType() == typeof(T1) || @event.GetType() == typeof(T2);

    public static bool IsEvent<T1, T2, T3>(this IntegrationEvent @event)
        => @event.GetType() == typeof(T1) || @event.GetType() == typeof(T2) || @event.GetType() == typeof(T3);

    public static bool IsEvent<T1, T2, T3, T4>(this IntegrationEvent @event)
        => @event.GetType() == typeof(T1) || @event.GetType() == typeof(T2)
        || @event.GetType() == typeof(T3) || @event.GetType() == typeof(T4);
}
