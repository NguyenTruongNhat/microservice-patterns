# EventBus.Kafka — Structure Reference

> **Role:** Kafka implementation of the `EventBus` abstraction layer.
> Provides producer, consumer, and DI registration helpers.
> Target framework: **.NET 9**

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Confluent.Kafka` | 2.10.0 | Kafka producer/consumer client |
| `Microsoft.Extensions.Hosting.Abstractions` | 9.0.5 | `BackgroundService`, `IHostApplicationBuilder` |
| `EventBus` _(project ref)_ | — | Abstractions and base types |

> MediatR is available via the `EventBus` project reference (transitive).

---

## Folder Structure

```
EventBus.Kafka/
├── EventHandlingService.cs         # BackgroundService: consumes Kafka → dispatches via MediatR
├── KafkaEventPublisher.cs          # IEventPublisher implementation over Kafka
├── KafkaEventBusExtensions.cs      # DI registration extensions + IsEvent<T> helpers
└── GlobalUsings.cs                 # Global using directives
```

---

## Key Types

### `KafkaEventPublisher` — Kafka Producer

```csharp
public class KafkaEventPublisher(
    string topic,
    IProducer<string, MessageEnvelop> producer,
    ILogger logger) : IEventPublisher
```

**Publish flow:**

```
PublishAsync<TEvent>(event)
    1. JsonSerializer.Serialize(event, event.GetType())  → JSON string
    2. Wrap in MessageEnvelop { MessageTypeName = type.FullName, Message = json }
    3. producer.ProduceAsync(topic, Message { Key = type.FullName, Value = envelop })
    4. Returns true on success, false + logs error on exception
```

- Registered as **Transient** via `AddKafkaEventPublisher`.
- One publisher instance per topic.
- Logger name format: `EventPublisher<{topic}>`.

---

### `EventHandlingService` — Kafka Consumer (BackgroundService)

```csharp
public class EventHandlingService : BackgroundService
```

**Consume flow:**

```
ExecuteAsync (loop)
    1. consumer.Subscribe(options.Topics)
    2. consumer.Consume(100ms timeout) → ConsumeResult<string, MessageEnvelop>
    3. IIntegrationEventFactory.CreateEvent(typeName, json) → IntegrationEvent?
    4. options.AcceptEvent(event) → bool  (filter unwanted events)
    5. IServiceScope → IMediator.Send(event)  ← MediatR dispatch to IRequestHandler<TEvent>
```

**Error handling:**
- Inner loop catches per-message exceptions → logs error, continues consuming.
- Outer loop catches subscribe failures → waits 1 second, retries.
- `consumer.Consume(100)` is non-blocking (100ms poll) → checks `stoppingToken` frequently.

> ⚠️ `IServiceScopeFactory` is used to create a new DI scope per message.
> Required because `EventHandlingService` is **Singleton** but `IMediator` handlers are typically **Scoped**.

---

### `EventHandlingWorkerOptions` — Consumer Configuration

```csharp
public class EventHandlingWorkerOptions
{
    public string KafkaGroupId { get; set; } = "event-handling";
    public List<string> Topics { get; set; } = [];
    public IIntegrationEventFactory IntegrationEventFactory { get; set; }
        = IntegrationEventFactory.Instance;
    public string ServiceName { get; set; } = "EventHandlingService";
    public Func<IntegrationEvent, bool> AcceptEvent { get; set; } = _ => true;
}
```

| Property | Purpose |
|---|---|
| `KafkaGroupId` | Kafka consumer group ID — controls offset sharing between instances |
| `Topics` | List of Kafka topics this service subscribes to |
| `IntegrationEventFactory` | Resolves typed event from `MessageEnvelop` — use `IntegrationEventFactory<TEvent>` for better performance |
| `ServiceName` | Logger category name |
| `AcceptEvent` | Filter predicate — return `false` to skip events this service doesn't care about |

---

### `KafkaEventBusExtensions` — DI Registration

#### Producer Setup

```csharp
// 1. Register IProducer<string, MessageEnvelop> (Singleton)
services.ConfigureKafkaProducer(configuration);

// 2. Register IEventPublisher (Transient) for a specific topic
builder.AddKafkaEventPublisher(topic: "book-events");
```

Config key required in `appsettings.json`:
```json
{
  "KafkaConnection": "localhost:9092"
}
```

#### Consumer Setup

```csharp
builder.AddKafkaEventConsumer(options =>
{
    options.KafkaGroupId = "borrowing-service";
    options.Topics = ["book-events", "borrower-events"];
    options.IntegrationEventFactory = IntegrationEventFactory<BookCreatedEvent>.Instance;
    options.ServiceName = "BorrowingService.EventHandler";
    options.AcceptEvent = e => e.IsEvent<BookCreatedEvent, BorrowerCreatedEvent>();
});
```

Internally registers:
- `AddKafkaMessageEnvelopConsumer` → `IConsumer<string, MessageEnvelop>` (Singleton)
- `EventHandlingWorkerOptions` (Singleton)
- `IIntegrationEventFactory` (Singleton, from options)
- `EventHandlingService` as `IHostedService` (Singleton BackgroundService)

#### `IsEvent<T>` Extension Helpers

```csharp
@event.IsEvent<BookCreatedEvent>()
@event.IsEvent<BookCreatedEvent, BorrowerCreatedEvent>()
@event.IsEvent<T1, T2, T3>()
@event.IsEvent<T1, T2, T3, T4>()
```

- Convenience helpers for `AcceptEvent` filter lambda.
- Overloads support up to 4 event types.

---

### Internal Serializers

| Class | Direction | Mechanism |
|---|---|---|
| `MessageEnvelopSerializer` | Produce (serialize) | `JsonSerializer.SerializeToUtf8Bytes(envelop)` |
| `MessageEnvelopDeserializer` | Consume (deserialize) | `JsonSerializer.Deserialize<MessageEnvelop>(data)` |

Both are `internal` — registered automatically inside `ConfigureKafkaProducer` and `AddKafkaMessageEnvelopConsumer`.

---

## Full Message Flow

```
[Producer Service]
    IEventPublisher.PublishAsync(new BookCreatedEvent { ... })
        → KafkaEventPublisher
            → JSON serialize → MessageEnvelop
                → Kafka Topic: "book-events"
                    Key   = "CQRS.Library.IntegrationEvents.BookCreatedEvent"
                    Value = { MessageTypeName: "...", Message: "{...json...}" }

[Consumer Service]
    EventHandlingService (BackgroundService, loop every 100ms)
        → consumer.Consume()
            → MessageEnvelop
                → IntegrationEventFactory.CreateEvent(typeName, json)
                    → BookCreatedEvent instance
                        → AcceptEvent(event) == true?
                            → IServiceScope.IMediator.Send(event)
                                → BookCreatedEventHandler.Handle(event)
```

---

## Design Rules

- **Never** reference `EventBus.Kafka` from `EventBus` — dependency is one-way.
- Services reference `EventBus` (abstractions) + `EventBus.Kafka` (for DI registration in `Program.cs`).
- Always use `IntegrationEventFactory<TEvent>` (generic version) in `AcceptEvent` options for type-safe resolution.
- `EnableAutoCommit = true` — offsets are committed automatically; ensure handlers are idempotent.
