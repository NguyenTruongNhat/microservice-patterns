# EventBus — Structure Reference

> **Role:** Pure abstraction layer. Defines contracts and base types for the messaging system.
> No infrastructure dependency (no Kafka, no RabbitMQ). Target framework: **.NET 9**

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `MediatR` | 12.5.0 | `IntegrationEvent` implements `IRequest` for in-process dispatch |
| `Microsoft.Extensions.Logging.Abstractions` | 9.0.5 | `ILogger` for `NullEventPublisher` |

---

## Folder Structure

```
EventBus/
├── Abstractions/
│   ├── IEventPublisher.cs              # Contract for publishing events
│   ├── IIntegrationEventHandler.cs     # Contract for handling events
│   └── MessageEnvelop.cs              # Kafka message wrapper DTO
├── Events/
│   └── IntegrationEvent.cs            # Base class for all integration events
├── IIntegrationEventFactory.cs         # Contract for deserializing events by type name
├── IntegrationEventFactory.cs          # Default + generic implementations
└── NullEventPublisher.cs              # No-op publisher (Null Object Pattern)
```

---

## Key Types

### `IntegrationEvent` — Base Event Class

```csharp
public class IntegrationEvent : IRequest
{
    public Guid EventId { get; }            // Guid v7 (time-ordered)
    public DateTime EventCreationDate { get; } // UTC timestamp
}
```

- **All integration events must inherit from this class.**
- Implements `IRequest` (MediatR) → handlers implement `IRequestHandler<TEvent>`.
- `EventId` uses `Guid.CreateVersion7()` — monotonically increasing, ideal for ordering.

---

### `IEventPublisher` — Publish Contract

```csharp
public interface IEventPublisher
{
    Task<bool> PublishAsync<TEvent>(TEvent @event) where TEvent : IntegrationEvent;
}
```

- Returns `true` on success, `false` on failure (no exception thrown to caller).
- Inject this in services that need to publish events.

---

### `IIntegrationEventHandler<T>` — Handler Contract

```csharp
public interface IIntegrationEventHandler<T> where T : IntegrationEvent
{
    Task Handle(T @event);
}
```

> ⚠️ In practice, handlers implement **MediatR's `IRequestHandler<TEvent>`** directly (since `IntegrationEvent : IRequest`).
> This interface is informational / for documentation purposes.

---

### `MessageEnvelop` — Transport Wrapper

```csharp
public class MessageEnvelop
{
    public string MessageTypeName { get; set; }  // e.g. "CQRS.Library.IntegrationEvents.BookCreatedEvent"
    public string Message { get; set; }          // JSON-serialized event payload
}
```

- Used as the Kafka **message value** when transmitting events.
- `MessageTypeName` = `type.FullName` — used by the consumer to resolve the concrete event type.
- Kafka message **key** = `event.GetType().FullName`.

---

### `IIntegrationEventFactory` / `IntegrationEventFactory`

```csharp
public interface IIntegrationEventFactory
{
    IntegrationEvent? CreateEvent(string typeName, string value);
}
```

Two implementations:

| Class | Type Resolution Strategy |
|---|---|
| `IntegrationEventFactory` | `Type.GetType()` first → fallback: scan all loaded assemblies in `AppDomain` |
| `IntegrationEventFactory<TEvent>` | Scans `typeof(TEvent).Assembly` first → then `Type.GetType()` → then full AppDomain scan |

- Use `IntegrationEventFactory<TEvent>.Instance` when events are defined in a specific assembly (e.g., `CQRS.Library.IntegrationEvents`) for better performance.
- Use `IntegrationEventFactory.Instance` (non-generic) as a general fallback.

---

### `NullEventPublisher` — Null Object Pattern

```csharp
public sealed class NullEventPublisher : IEventPublisher
{
    public Task<bool> PublishAsync<TEvent>(TEvent @event) where TEvent : IntegrationEvent
        => Task.FromResult(true);
}
```

- Register when a service **only consumes** events and never publishes.
- Logs a warning at startup: `"NullEventPublisher is used"` — useful to catch misconfiguration.
- Prevents null reference errors if code accidentally calls `IEventPublisher`.

---

## Design Rules

- **Do not add infrastructure packages** to `EventBus.csproj`. Keep it dependency-free from messaging brokers.
- All new event types **must** inherit `IntegrationEvent`.
- Place event definitions in a dedicated `*.IntegrationEvents` project (not here).
- This project is referenced by both `EventBus.Kafka` and all consumer/producer services.

---

## How to Use (Core Workflow)

### 1. Define an Integration Event
Create a class that inherits from `IntegrationEvent`. Place this in a shared project (e.g., `MyApp.IntegrationEvents`) so both publisher and consumer can reference it.

```csharp
using EventBus.Events;

public class CustomGreetingEvent : IntegrationEvent
{
    public string Message { get; set; }
    public string UserName { get; set; }

    public CustomGreetingEvent(string message, string userName)
    {
        Message = message;
        UserName = userName;
    }
}
```

### 2. Publish the Event
Inject `IEventPublisher` into your service or controller and call `PublishAsync`.

```csharp
using EventBus.Abstractions;

public class GreetingService
{
    private readonly IEventPublisher _eventPublisher;

    public GreetingService(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    public async Task SendGreetingAsync()
    {
        var @event = new CustomGreetingEvent("Hello, EventBus!", "User123");
        
        bool success = await _eventPublisher.PublishAsync(@event);
        if (!success)
        {
            // Handle publish failure
        }
    }
}
```

### 3. Handle the Event
Create a handler that implements MediatR's `IRequestHandler<TEvent>`. The `EventHandlingService` (from Kafka/RabbitMQ) will automatically resolve this handler inside a separate DI scope when the event arrives.

```csharp
using MediatR;
using Microsoft.Extensions.Logging;

public class CustomGreetingEventHandler : IRequestHandler<CustomGreetingEvent>
{
    private readonly ILogger<CustomGreetingEventHandler> _logger;

    public CustomGreetingEventHandler(ILogger<CustomGreetingEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(CustomGreetingEvent request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received greeting for {User}: {Message}", request.UserName, request.Message);
        
        // Process the event (e.g., update DB, send email)...

        return Task.CompletedTask;
    }
}
```

### 4. Configure Services (Program.cs)

Depending on the broker you're using, you register the publisher or consumer in your service's `Program.cs`. Because the contracts are identical, switching between Kafka and RabbitMQ only requires changing the registration method.

#### Option A: RabbitMQ Setup

```csharp
using EventBus.RabbitMQ;

// ----- For a Producer Service -----
builder.Services.ConfigureRabbitMQConnection(builder.Configuration);
builder.AddRabbitMQEventPublisher(exchangeName: "integration-events");

// ----- For a Consumer Service -----
builder.Services.ConfigureRabbitMQConnection(builder.Configuration);
// Need to add MediatR to resolve the handlers
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

builder.AddRabbitMQEventConsumer(options =>
{
    options.ExchangeName = "integration-events";        // Must match publisher's exchange
    options.QueueName    = "borrowing-service-queue";   // Unique queue for this consumer group
    options.ServiceName  = "BorrowingService.EventHandler";
    options.AcceptEvent  = e => e.IsEvent<CustomGreetingEvent>(); // Filter events to handle
});
```

#### Option B: Kafka Setup

```csharp
using EventBus.Kafka;

// ----- For a Producer Service -----
builder.Services.ConfigureKafkaProducer(builder.Configuration);
builder.AddKafkaEventPublisher(topic: "integration-events");

// ----- For a Consumer Service -----
// Need to add MediatR to resolve the handlers
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

builder.AddKafkaEventConsumer(options =>
{
    options.KafkaGroupId = "borrowing-service-group";
    options.Topics       = new List<string> { "integration-events" }; // Must match publisher's topic
    options.ServiceName  = "BorrowingService.EventHandler";
    options.AcceptEvent  = e => e.IsEvent<CustomGreetingEvent>(); // Filter events to handle
});
```

---

## Resilience & Reliability (Production Features)

### 1. Resilient Event Publisher (Retry + Circuit Breaker)

The `ResilientEventPublisher` wraps any `IEventPublisher` (Kafka/RabbitMQ) with Polly v8 to provide transient fault handling and circuit breaker protection.

**Usage in `Program.cs`:**
```csharp
// 1. Register base publisher
builder.Services.ConfigureRabbitMQConnection(builder.Configuration);
builder.AddRabbitMQEventPublisher("integration-events");

// 2. Decorate with Resilience (Retry + Circuit Breaker)
builder.Services.AddResilientEventPublisher(options =>
{
    options.MaxRetryAttempts = 3;
    options.RetryBaseDelay = TimeSpan.FromSeconds(1);
    options.CircuitBreakerFailureRatio = 0.5;
    options.CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30);
});
// Now, injecting IEventPublisher will inject the ResilientEventPublisher.
```

### 2. Idempotent Consumer

By default, an `InMemoryIdempotencyStore` is registered to prevent duplicate event processing (e.g., if a broker redelivers an un-acked message).

**Custom Store Override:**
If you need distributed idempotency (e.g., using Redis or a Database), register your own `IIdempotencyStore` **before** adding the consumer:

```csharp
// Custom implementation
public class RedisIdempotencyStore : IIdempotencyStore { ... }

// Program.cs
builder.Services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>(); // Will override the default

// Register consumer as usual
builder.AddRabbitMQEventConsumer(...);
```

### 3. Dead Letter Queue (DLQ) & In-Memory Consume Retries

Both RabbitMQ and Kafka consumers include an in-memory retry loop. If a message fails to process after `MaxRetryAttempts`, it is routed to a Dead Letter Exchange/Topic.

**Configuration:**
```csharp
builder.AddRabbitMQEventConsumer(options =>
{
    options.MaxRetryAttempts = 3;                         // Retry 3 times internally
    options.DeadLetterExchange = "integration-events.dlx"; // Move to DLX on failure
});
// Note: Kafka uses options.DeadLetterTopic instead of DeadLetterExchange.
```
