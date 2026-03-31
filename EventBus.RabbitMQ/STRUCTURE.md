# EventBus.RabbitMQ — Structure Reference

> **Role:** RabbitMQ implementation of the `EventBus` abstraction layer.
> Drop-in replacement for `EventBus.Kafka` — identical contracts, same wire format (`MessageEnvelop`).
> Target framework: **.NET 9**

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `RabbitMQ.Client` | 7.1.2 | RabbitMQ async producer/consumer client |
| `Microsoft.Extensions.Hosting.Abstractions` | 9.0.5 | `BackgroundService`, `IHostApplicationBuilder` |
| `EventBus` _(project ref)_ | — | Abstractions and base types |

> MediatR is available via the `EventBus` project reference (transitive).

---

## Folder Structure

```
EventBus.RabbitMQ/
├── RabbitMQSettings.cs              # Strongly-typed config (HostName, Port, UserName, Password, VHost)
├── RabbitMQEventPublisher.cs        # IEventPublisher implementation over RabbitMQ topic exchange
├── EventHandlingService.cs          # BackgroundService: consumes queue → dispatches via MediatR
├── RabbitMQEventBusExtensions.cs    # DI registration extensions + IsEvent<T> helpers
└── GlobalUsings.cs                  # Global using directives
```

---

## Kafka ↔ RabbitMQ Concept Mapping

| Kafka Concept | RabbitMQ Equivalent | Implementation Detail |
|---|---|---|
| **Topic** | Exchange (type: `topic`, durable) | `ExchangeName` in options |
| **Message Key** | Routing key | `event.GetType().FullName` |
| **Consumer Group** | Queue (shared by same-name instances) | `QueueName` in options |
| **Partition subscribe** | Queue bind with routing key `#` | Wildcard = receive all events |
| **GroupId** | `QueueName` | Same queue name = load-balanced group |
| **AutoOffsetReset.Earliest** | `autoAck: false` + manual `BasicAck` | Explicit ack on success |
| **EnableAutoCommit** | `BasicNack` + requeue on failure | Requeue failed messages |
| **`KafkaConnection`** config key | `RabbitMQ` config section | `RabbitMQSettings` |

**Wire format is identical:** Both use `MessageEnvelop { MessageTypeName, Message }` as the message body (JSON).

---

## Key Types

### `RabbitMQSettings` — Connection Configuration

```csharp
public class RabbitMQSettings
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
}
```

Maps from `appsettings.json`:
```json
{
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  }
}
```

---

### `RabbitMQEventPublisher` — RabbitMQ Producer

```csharp
public class RabbitMQEventPublisher(
    string exchangeName,
    IChannel channel,
    ILogger logger) : IEventPublisher
```

**Publish flow:**

```
PublishAsync<TEvent>(event)
    1. JsonSerializer.Serialize(event, event.GetType())  → JSON string
    2. Wrap in MessageEnvelop { MessageTypeName = type.FullName, Message = json }
    3. JsonSerializer.SerializeToUtf8Bytes(envelop)      → byte[]
    4. BasicProperties { ContentType, DeliveryMode=Persistent, MessageId, Timestamp }
    5. SemaphoreSlim.WaitAsync()  ← IChannel is NOT thread-safe in RabbitMQ.Client v7
    6. channel.BasicPublishAsync(exchange, routingKey=type.FullName, props, body)
    7. SemaphoreSlim.Release()
    8. Returns true on success, false + logs error on exception
```

> ⚠️ **Thread-safety:** `IChannel` in RabbitMQ.Client v7 is **not thread-safe**.
> `RabbitMQEventPublisher` uses `SemaphoreSlim(1,1)` to serialize concurrent publish calls.
> This differs from Kafka's `IProducer` which is thread-safe internally.

---

### `EventHandlingService` — RabbitMQ Consumer (BackgroundService)

```csharp
public class EventHandlingService : BackgroundService
```

**Startup sequence:**
```
ExecuteAsync
    1. connection.CreateChannelAsync()         → dedicated consumer IChannel
    2. channel.ExchangeDeclareAsync(topic, durable=true)
    3. channel.QueueDeclareAsync(queueName, durable=true)
    4. channel.QueueBindAsync(queue, exchange, routingKey="#")   ← wildcard: all events
    5. channel.BasicQosAsync(prefetchCount=1)  ← one message at a time
    6. AsyncEventingBasicConsumer.ReceivedAsync += handler
    7. channel.BasicConsumeAsync(autoAck=false)
    8. Task.Delay(Infinite, stoppingToken)     ← hold alive
```

**Per-message handler:**
```
ReceivedAsync
    1. Deserialize MessageEnvelop from ea.Body.Span
    2. IServiceScope → IMediator  (new scope per message, same as Kafka)
    3. IIntegrationEventFactory.CreateEvent(typeName, json) → IntegrationEvent?
    4. options.AcceptEvent(event) == true?
        → mediator.Send(event)
        → channel.BasicAckAsync(deliveryTag)
    5. On exception:
        → channel.BasicNackAsync(deliveryTag, requeue=true)
```

> ⚠️ `IServiceScopeFactory` is required because `EventHandlingService` is **Singleton**
> but `IMediator` handlers are typically **Scoped**.

---

### `EventHandlingWorkerOptions` — Consumer Configuration

```csharp
public class EventHandlingWorkerOptions
{
    public string ExchangeName { get; set; } = "integration-events";
    public string QueueName { get; set; } = "event-handling";
    public IIntegrationEventFactory IntegrationEventFactory { get; set; }
        = IntegrationEventFactory.Instance;
    public string ServiceName { get; set; } = "EventHandlingService";
    public Func<IntegrationEvent, bool> AcceptEvent { get; set; } = _ => true;
}
```

| Property | Kafka Equivalent | Notes |
|---|---|---|
| `ExchangeName` | Topic name group | Exchange shared by all producing services |
| `QueueName` | `GroupId` + subscription | Unique per service; same name = consumer group |
| `IntegrationEventFactory` | `IntegrationEventFactory` | Use generic `<TEvent>` version for perf |
| `ServiceName` | `ServiceName` | Logger category name |
| `AcceptEvent` | `AcceptEvent` | Filter: identical API via `IsEvent<T>` helpers |

---

### `RabbitMQEventBusExtensions` — DI Registration

#### Producer Setup

```csharp
// Program.cs
builder.Services.ConfigureRabbitMQConnection(builder.Configuration);   // IConnection + IChannel (Singleton)
builder.AddRabbitMQEventPublisher(exchangeName: "integration-events");   // IEventPublisher (Singleton)
```

#### Consumer Setup

```csharp
// Program.cs
builder.Services.ConfigureRabbitMQConnection(builder.Configuration);

builder.AddRabbitMQEventConsumer(options =>
{
    options.ExchangeName          = "integration-events";
    options.QueueName             = "borrowing-service-queue";    // unique per service
    options.IntegrationEventFactory = IntegrationEventFactory<BookCreatedEvent>.Instance;
    options.ServiceName           = "BorrowingService.EventHandler";
    options.AcceptEvent           = e => e.IsEvent<BookCreatedEvent, BorrowerCreatedEvent>();
});
```

#### `IsEvent<T>` Helpers (identical to Kafka)

```csharp
e.IsEvent<BookCreatedEvent>()
e.IsEvent<BookCreatedEvent, BorrowerCreatedEvent>()
e.IsEvent<T1, T2, T3>()
e.IsEvent<T1, T2, T3, T4>()
```

---

## Full Message Flow

```
[Producer Service]
    IEventPublisher.PublishAsync(new BookCreatedEvent { ... })
        → RabbitMQEventPublisher
            → JSON serialize → MessageEnvelop { MessageTypeName, Message }
                → channel.BasicPublishAsync(
                        exchange: "integration-events",
                        routingKey: "CQRS.Library.IntegrationEvents.BookCreatedEvent",
                        body: JSON(MessageEnvelop))

[Consumer Service]
    EventHandlingService (BackgroundService)
        → AsyncEventingBasicConsumer.ReceivedAsync
            → Deserialize MessageEnvelop
                → IntegrationEventFactory.CreateEvent(typeName, json)
                    → BookCreatedEvent instance
                        → AcceptEvent(event) == true?
                            → IServiceScope.IMediator.Send(event)
                                → BookCreatedEventHandler.Handle(event)
                            → BasicAckAsync ✅
                        (on exception) → BasicNackAsync + requeue ♻️
```

---

## Design Rules

- **Do not** reference `EventBus.RabbitMQ` from `EventBus` — dependency is one-way.
- Services reference `EventBus` (abstractions only) + `EventBus.RabbitMQ` in `Program.cs` for DI.
- Always use `IntegrationEventFactory<TEvent>` (assembly-scoped) over the non-generic version.
- **`autoAck: false` is mandatory** — enables exactly-once processing with manual Ack/Nack.
- The publish `IChannel` and the consume `IChannel` are **separate instances** — never share them.
- `ExchangeName` must match between publisher and consumer — treat it as the Kafka topic name.
