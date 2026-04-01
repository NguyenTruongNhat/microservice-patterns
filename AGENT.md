# AGENT.md — AI Coding Assistant Guidelines

> **Purpose:** This document serves as the single source of truth for all AI coding assistants (GitHub Copilot, ChatGPT, Claude, Gemini, etc.) working on this project. Follow these rules strictly to ensure consistency, quality, and maintainability across the codebase.

---

## Table of Contents

- [1. Project Overview](#1-project-overview)
- [2. Coding Standards](#2-coding-standards)
- [3. Backend Guidelines (.NET)](#3-backend-guidelines-net)
- [4. Database & Performance](#4-database--performance)
- [5. API Design](#5-api-design)
- [6. DevOps & Deployment](#6-devops--deployment)
- [7. Testing](#7-testing)
- [8. AI Agent Rules](#8-ai-agent-rules)
- [9. Communication Style](#9-communication-style)

---

## 1. Project Overview

### Description

**MicroservicePatterns** is a learning-oriented .NET solution that demonstrates real-world microservice architecture patterns through practical implementations. Each pattern is built as an isolated, runnable set of services under the `patterns/` directory.

### Implemented Patterns

| Pattern | Domain Example | Path |
|---|---|---|
| **CQRS** | Library Management | `patterns/CQRS/` |
| **Saga — Choreography** | Online Store (Order → Payment → Inventory) | `patterns/Saga/Choreography/` |
| **Saga — Orchestration** | Trip Planner (Hotel + Ticket + Payment) | `patterns/Saga/Orchestrtion/` |

### Tech Stack

| Layer | Technology |
|---|---|
| Language | C# / .NET 8+ |
| Web Framework | ASP.NET Core Minimal APIs / MVC |
| Messaging | Apache Kafka (Confluent) |
| Containerization | Docker / Docker Compose |
| Database | SQL Server / PostgreSQL (per-service) |
| Shared Libraries | `EventBus`, `EventBus.Kafka`, `MicroservicePatterns.Shared` |

### Solution Structure

```
MicroservicePatterns/
├── AGENT.md                                    # ← You are here
├── MicroservicePatterns.sln
├── docker-compose.yaml                         # Kafka + Zookeeper + Kafka UI
├── MicroservicePatterns.Shared/                # Cross-cutting concerns
├── MicroservicePatterns.DatabaseMigrationHelpers/
├── EventBus/                                   # Abstraction layer for messaging
├── EventBus.Kafka/                             # Kafka implementation of EventBus
├── KafkaConsumerInitializationService/         # Kafka consumer bootstrapper
├── patterns/
│   ├── CQRS/                                   # CQRS pattern services
│   │   ├── CQRS.Library.BookService/
│   │   ├── CQRS.Library.BorrowerService/
│   │   ├── CQRS.Library.BorrowingService/
│   │   ├── CQRS.Library.BorrowingHistoryService/
│   │   └── CQRS.Library.IntegrationEvents/
│   └── Saga/
│       ├── Choreography/                       # Saga via event-driven choreography
│       │   ├── Saga.OnlineStore.OrderService/
│       │   ├── Saga.OnlineStore.PaymentService/
│       │   ├── Saga.OnlineStore.InventoryService/
│       │   ├── Saga.OnlineStore.CatalogService/
│       │   └── Saga.OnlineStore.IntegrationEvents/
│       └── Orchestrtion/                       # Saga via central orchestrator
│           ├── Saga.TripPlanner.TripPlanningService/
│           ├── Saga.TripPlanner.HotelService/
│           ├── Saga.TripPlanner.TicketService/
│           └── Saga.TripPlanner.PaymentService/
└── http patterns/                              # HTTP request files for testing
```

---

## 2. Coding Standards

### Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Namespace | PascalCase, match folder path | `CQRS.Library.BookService.Controllers` |
| Class / Record | PascalCase | `BookController`, `OrderCreatedEvent` |
| Interface | `I` + PascalCase | `IBookRepository`, `IEventBus` |
| Method | PascalCase | `GetBookByIdAsync()` |
| Property | PascalCase | `public string Title { get; set; }` |
| Private field | `_camelCase` | `private readonly ILogger _logger;` |
| Local variable | camelCase | `var bookList = ...;` |
| Constant | PascalCase | `const int MaxRetryCount = 3;` |
| Async method | Suffix with `Async` | `CreateOrderAsync()` |
| Event class | Past-tense verb | `OrderCreatedEvent`, `PaymentFailedEvent` |

### Clean Code Principles

- **Single Responsibility:** One class = one reason to change.
- **Small Methods:** Each method should do one thing and do it well. Aim for ≤ 20 lines.
- **Meaningful Names:** Names should reveal intent — avoid abbreviations like `mgr`, `svc`, `dto2`.
- **No Magic Numbers:** Use named constants or enums.
- **No Dead Code:** Remove commented-out code, unused usings, and unreachable blocks.
- **DRY (Don't Repeat Yourself):** Extract shared logic into helper classes or shared libraries.

### SOLID Principles

| Principle | Guideline |
|---|---|
| **S** — Single Responsibility | Each service, controller, and handler should own one concern. |
| **O** — Open/Closed | Extend behavior via new classes/handlers, not by modifying existing ones. |
| **L** — Liskov Substitution | Derived types must be fully substitutable for their base types. |
| **I** — Interface Segregation | Prefer small, focused interfaces (e.g., `ICommandHandler<T>`, `IQueryHandler<T>`). |
| **D** — Dependency Inversion | Depend on abstractions (`IEventBus`), not concretions (`KafkaEventBus`). |

### Folder Structure Conventions (Per Service)

```
ServiceName/
├── Controllers/          # API endpoints (thin layer)
├── Services/             # Business logic
├── Repositories/         # Data access
├── Models/
│   ├── Entities/         # Domain / DB entities
│   ├── DTOs/             # Data transfer objects
│   └── Requests/         # Request models
├── Events/               # Integration / domain events
├── Handlers/             # Event & command handlers
├── Extensions/           # Service registration helpers
├── Migrations/           # EF Core migrations
├── Program.cs            # Entry point
└── appsettings.json      # Configuration
```

> **Note:** Not every service needs all folders. Create only what is needed — keep it lean.

---

## 3. Backend Guidelines (.NET)

### Controller → Service → Repository Pattern

```
[HTTP Request]
    → Controller (validation, mapping)
        → Service (business logic, orchestration)
            → Repository (data access, EF Core)
```

- **Controller:** Thin. Validate input, call service, return response. No business logic.
- **Service:** All business rules live here. Orchestrate repositories and event publishing.
- **Repository:** Data access only. Use EF Core or Dapper. No business logic leakage.

### Dependency Injection Best Practices

- Register services in `Program.cs` or a dedicated `Extensions/ServiceCollectionExtensions.cs`.
- Use appropriate lifetimes:
  - `Scoped` — per-request services (repositories, DbContext).
  - `Singleton` — stateless services (Kafka producers, configuration wrappers).
  - `Transient` — lightweight, stateless helpers.
- **Never** resolve services manually with `IServiceProvider` unless absolutely necessary.
- Prefer constructor injection. Avoid the Service Locator anti-pattern.

### Exception Handling

- Use a **global exception middleware** to catch unhandled exceptions.
- Define custom exception types for domain-specific errors:
  ```csharp
  public class EntityNotFoundException : Exception { }
  public class BusinessRuleViolationException : Exception { }
  ```
- **Do not** swallow exceptions silently (`catch { }`).
- Return consistent error responses (see [API Design](#5-api-design)).

### Logging Strategy

- Use `ILogger<T>` via constructor injection.
- Log levels:
  | Level | Usage |
  |---|---|
  | `Debug` | Development-only details |
  | `Information` | Key business events (order created, payment processed) |
  | `Warning` | Recoverable issues (retry, timeout) |
  | `Error` | Unrecoverable failures (exceptions, broken dependencies) |
- Use **structured logging** with message templates:
  ```csharp
  _logger.LogInformation("Order {OrderId} created for customer {CustomerId}", orderId, customerId);
  ```
- **Never** log sensitive data (passwords, tokens, PII).

---

## 4. Database & Performance

### SQL Best Practices

- Each microservice **owns its own database** — no shared databases.
- Use **EF Core Migrations** for schema changes. Never alter schemas manually in production.
- Use `AsNoTracking()` for read-only queries.
- Avoid `N+1` query problems — use `Include()` / `ThenInclude()` or projection.

### Query Optimization

- Prefer **projection** (`Select(x => new { ... })`) over loading full entities.
- Use pagination (`Skip` + `Take`) for list endpoints.
- Avoid `ToListAsync()` before filtering — apply filters in the query.
- Profile queries with EF Core logging or SQL Server Profiler.

### Indexing Guidelines

- Index columns used in `WHERE`, `JOIN`, and `ORDER BY` clauses.
- Use **composite indexes** for multi-column filters.
- Avoid over-indexing — each index adds write overhead.
- Add indexes via EF Core Fluent API:
  ```csharp
  builder.HasIndex(b => b.Isbn).IsUnique();
  builder.HasIndex(b => new { b.Status, b.CreatedAt });
  ```

---

## 5. API Design

### RESTful Conventions

| Action | HTTP Method | Route Example | Status Code |
|---|---|---|---|
| List | `GET` | `/api/books` | `200 OK` |
| Get by ID | `GET` | `/api/books/{id}` | `200 OK` / `404` |
| Create | `POST` | `/api/books` | `201 Created` |
| Update | `PUT` | `/api/books/{id}` | `200 OK` / `204` |
| Partial Update | `PATCH` | `/api/books/{id}` | `200 OK` |
| Delete | `DELETE` | `/api/books/{id}` | `204 No Content` |

### Request / Response Format

- Use **JSON** as the default content type.
- Wrap responses in a consistent envelope when appropriate:
  ```json
  {
    "success": true,
    "data": { ... },
    "errors": []
  }
  ```
- Use `DateTimeOffset` (ISO 8601) for all date/time fields.

### Error Handling Standard

Return errors in a consistent structure:
```json
{
  "success": false,
  "data": null,
  "errors": [
    {
      "code": "BOOK_NOT_FOUND",
      "message": "Book with ID 42 was not found."
    }
  ]
}
```

- Use appropriate HTTP status codes:
  - `400` — Validation errors
  - `404` — Resource not found
  - `409` — Conflict (duplicate, concurrency)
  - `500` — Unhandled server error

---

## 6. DevOps & Deployment

### Docker Usage

- Infrastructure services (Kafka, Zookeeper, Kafka UI) are defined in `docker-compose.yaml`.
- Application services are run individually via `dotnet run` or through the solution in your IDE.
- When adding new infrastructure, add it to `docker-compose.yaml` with:
  - Explicit `container_name`
  - Port mappings
  - Health checks where possible
  - `depends_on` for startup ordering

### Environment Configuration

- Use `appsettings.json` → `appsettings.{Environment}.json` hierarchy.
- Store secrets in **User Secrets** (development) or environment variables (production).
- **Never** commit secrets, connection strings, or API keys to source control.
- Access configuration via `IOptions<T>` pattern:
  ```csharp
  services.Configure<KafkaSettings>(configuration.GetSection("Kafka"));
  ```

### CI/CD Notes

- Ensure all projects build cleanly with `dotnet build`.
- Run tests with `dotnet test` before merging.
- Follow **conventional commits** for commit messages:
  ```
  feat(cqrs): add book search endpoint
  fix(saga): handle payment timeout in orchestrator
  docs: update AGENT.md with testing guidelines
  ```

---

## 7. Testing

### Unit Test Conventions

- Test project naming: `{ServiceName}.Tests`
- Use **xUnit** as the testing framework.
- Use **Moq** or **NSubstitute** for mocking dependencies.
- Follow the **Arrange → Act → Assert** pattern.
- Name tests descriptively:
  ```
  MethodName_Scenario_ExpectedResult
  ```
  Example: `CreateOrder_WhenInventoryInsufficient_ThrowsBusinessRuleException`
- Test business logic in the **Service layer**, not controllers.

### Integration Testing Approach

- Use `WebApplicationFactory<T>` for API integration tests.
- Use **Testcontainers** for database and Kafka dependencies.
- Test inter-service communication flows (event publishing → consuming).
- Keep integration tests isolated — each test should set up and tear down its own data.

---

## 8. AI Agent Rules

> ⚠️ **These rules are mandatory for all AI coding assistants.**

### Code Generation Rules

1. **Follow existing code style** — Match the patterns, naming, and structure already in place. When in doubt, look at neighboring files.
2. **Do not introduce new libraries** unless explicitly approved or clearly necessary. Check the `.csproj` files for existing dependencies first.
3. **Prefer simple, maintainable solutions** — Avoid over-engineering. Choose readability over cleverness.
4. **Explain before generating complex code** — If the implementation involves non-trivial logic (Saga orchestration, event choreography, CQRS projections), provide a brief explanation before writing code.
5. **Ask for clarification** if the requirements are ambiguous or incomplete. Do not make assumptions about business rules.
6. **Respect the pattern boundaries** — Each pattern under `patterns/` is self-contained. Do not create cross-pattern dependencies.
7. **Use shared libraries correctly** — Use `EventBus` and `EventBus.Kafka` for messaging. Use `MicroservicePatterns.Shared` for cross-cutting concerns.
8. **Follow the service template** — New services should mirror the folder structure and conventions of existing services in the same pattern group.

### What NOT to Do

- ❌ Do not refactor unrelated code without being asked.
- ❌ Do not change the solution structure or move projects.
- ❌ Do not add NuGet packages without stating the reason.
- ❌ Do not use `var` excessively when the type is not obvious from context.
- ❌ Do not generate placeholder / TODO code — implement fully or ask for clarification.

---

## 9. Communication Style

- **Be concise** — Get to the point. Avoid filler text.
- **Use clear technical language** — Assume the reader is a developer familiar with .NET and microservices.
- **Show, don't tell** — Prefer code examples over lengthy explanations.
- **Structure your response** — Use headings, bullet points, and code blocks for readability.
- **Highlight trade-offs** — When multiple approaches exist, briefly list pros/cons.
- **Reference this document** — When a coding decision is guided by these rules, cite the relevant section.

---

*Last updated: 2026-03-31*
