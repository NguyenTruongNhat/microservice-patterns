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
