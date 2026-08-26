# QuizArena — BuildingBlocks Architecture Addendum

**Version:** 1.0.0
**Status:** Mandatory
**Applies to:** All QuizArena source projects

---

## 1. Existing BuildingBlocks Constraint

QuizArena MUST use the existing BuildingBlocks libraries available in the workspace.

The BuildingBlocks solution is an architectural dependency and MUST be treated as an existing platform capability rather than functionality to be reimplemented inside QuizArena.

The existing BuildingBlocks include:

```text
BuildingBlocks.Kernel.Domain
BuildingBlocks.CQRS
BuildingBlocks.EventBus
BuildingBlocks.EventBus.RabbitMQ
BuildingBlocks.Kernel.Infrastructure
BuildingBlocks.ServiceDefaults
```

These libraries provide the foundational capabilities for:

* DDD.
* Vertical Slice Architecture.
* CQRS.
* Domain Events.
* Integration Events.
* EventBus.
* RabbitMQ.
* Repository abstraction.
* Unit of Work.
* Specifications.
* EF Core infrastructure.
* Transactional Outbox.
* OpenTelemetry.
* Health checks.
* HTTP resilience.
* Endpoint registration.
* Result-to-HTTP mapping.
* Global exception handling.

---

# 2. No Reinvention Rule

QuizArena MUST NOT create alternative implementations of capabilities already provided by BuildingBlocks.

The following abstractions MUST be reused:

```text
Entity
AggregateRoot
ValueObject
StronglyTypedId
Enumeration

IDomainEvent
IDomainEventHandler
IBusinessRule

Result
Error

IRepository
IUnitOfWork
Specification<T>

ICommand
IQuery
ICommandHandler
IQueryHandler

ISender
IPipelineBehavior

IntegrationEvent
IEventBus
IIntegrationEventHandler

IOutboxWriter

IEndpoint
```

QuizArena MUST NOT introduce:

```text
MediatR
MassTransit
AutoMapper
```

for functionality already covered by BuildingBlocks.

---

# 3. BuildingBlocks Responsibility

BuildingBlocks provide technical and architectural infrastructure.

QuizArena provides business capabilities.

The separation MUST be:

```text
BuildingBlocks
    ↓
Technical capabilities

QuizArena
    ↓
Business capabilities
```

BuildingBlocks MUST NOT contain QuizArena-specific business rules.

QuizArena MUST NOT duplicate BuildingBlocks infrastructure.

---

# 4. Domain Layer

`QuizArena.Domain` MUST reference the BuildingBlocks domain kernel.

Domain entities SHOULD inherit or compose from the abstractions provided by:

```text
BuildingBlocks.Kernel.Domain
```

Examples:

```csharp
public sealed record GameId(Guid Value)
    : StronglyTypedId<Guid>(Value);
```

Aggregates SHOULD derive from the BuildingBlocks aggregate abstractions.

Domain events MUST implement the BuildingBlocks domain event contracts.

Business rules SHOULD use the BuildingBlocks business rule abstractions.

---

# 5. Application Layer

`QuizArena.Application` MUST use the BuildingBlocks CQRS infrastructure.

Features MUST follow Vertical Slice Architecture.

A feature SHOULD contain its own:

```text
Command or Query
Validator
Handler
Response DTO
Endpoint
```

Example:

```text
Features/
└── Games/
    └── SubmitAnswer.cs
```

The feature MUST NOT require a centralized generic command folder.

---

# 6. CQRS

The QuizArena application MUST use:

```text
ICommand<T>
IQuery<T>
ICommandHandler<TCommand,TResult>
IQueryHandler<TQuery,TResult>
ISender
IPipelineBehavior
```

from:

```text
BuildingBlocks.CQRS
```

No secondary dispatcher MUST be introduced.

No MediatR dependency MUST be added.

Validation and logging SHOULD use the existing BuildingBlocks pipeline behaviors.

---

# 7. Repository and Unit of Work

Persistence MUST use the abstractions provided by:

```text
BuildingBlocks.Kernel.Domain
BuildingBlocks.Kernel.Infrastructure
```

QuizArena SHOULD use:

```text
IRepository<TAggregate,TId>
IUnitOfWork
Specification<T>
```

The application layer MUST NOT depend on concrete EF Core repositories.

---

# 8. Specifications

Read and query requirements SHOULD use the BuildingBlocks `Specification<T>` abstraction whenever the query represents reusable domain-oriented filtering.

Specifications MAY use:

```text
Where
And
Or
Not
Include
Ordering
Pagination
ApplyAsNoTracking
```

The implementation MUST NOT create a second specification framework.

---

# 9. Persistence

QuizArena DbContexts SHOULD derive from:

```text
AppDbContextBase
```

provided by:

```text
BuildingBlocks.Kernel.Infrastructure
```

Domain events MUST participate in the existing `SaveChanges` lifecycle.

The transactional Outbox MUST use:

```text
IOutboxWriter
OutboxProcessor
```

provided by BuildingBlocks.

QuizArena MUST NOT implement an independent Outbox mechanism.

---

# 10. Domain Events

Domain events MUST remain in-process.

They SHOULD be dispatched by the existing BuildingBlocks domain event dispatcher.

Examples:

```text
GameStartedDomainEvent
RoundStartedDomainEvent
AnswerEvaluatedDomainEvent
PointsAwardedDomainEvent
PlayerWithdrawnDomainEvent
RoundCompletedDomainEvent
GameFinishedDomainEvent
```

Domain events MUST represent meaningful business facts and MUST NOT be used merely as technical notifications.

---

# 11. Integration Events

Integration events MUST use:

```text
IntegrationEvent
IEventBus
IIntegrationEventHandler
```

provided by:

```text
BuildingBlocks.EventBus
```

RabbitMQ transport MUST use:

```text
BuildingBlocks.EventBus.RabbitMQ
```

QuizArena MUST NOT introduce another messaging abstraction.

---

# 12. RabbitMQ

RabbitMQ MUST be used only where asynchronous integration provides architectural value.

The initial QuizArena implementation MUST NOT turn every domain event into a RabbitMQ message.

The following are candidates for integration events:

```text
GameFinished
RewardRedeemed
RewardGranted
GameStatisticsGenerated
NotificationRequested
```

Critical game state MUST remain persisted transactionally in the primary database.

RabbitMQ MUST NOT be the source of truth for game state.

---

# 13. At-Least-Once Delivery

Integration events MUST be assumed to have at-least-once delivery semantics.

Integration event handlers MUST therefore be idempotent.

Handlers MUST safely tolerate duplicate messages.

The implementation SHOULD use an idempotency key or event identifier to prevent duplicate processing.

---

# 14. Outbox Requirement

Whenever a domain operation modifies transactional state and requires an integration event, the operation SHOULD use the transactional Outbox.

The desired flow is:

```text
Command
   ↓
Domain operation
   ↓
Domain events
   ↓
Database transaction
   ├── Aggregate changes
   └── Outbox messages
             ↓
        Outbox Processor
             ↓
          RabbitMQ
```

The system MUST NOT publish an external integration event before the transactional database state has been safely committed.

---

# 15. Service Defaults

QuizArena hosts MUST use:

```text
BuildingBlocks.ServiceDefaults
```

for cross-cutting host capabilities where applicable.

This includes:

* OpenTelemetry.
* Logs.
* Distributed tracing.
* Metrics.
* Health checks.
* Liveness checks.
* HTTP resilience.
* Default endpoint conventions.
* Global exception handling.
* Result-to-HTTP mapping.

QuizArena MUST NOT create an alternative global observability framework without an explicit ADR.

---

# 16. Endpoint Architecture

HTTP endpoints MUST use:

```text
IEndpoint
```

from:

```text
BuildingBlocks.ServiceDefaults
```

Endpoints SHOULD remain thin.

The endpoint MUST NOT contain domain rules.

The endpoint SHOULD:

```text
Receive request
    ↓
Build Command/Query
    ↓
ISender.SendAsync()
    ↓
Map Result to HTTP
```

---

# 17. Result Handling

Application use cases SHOULD return the BuildingBlocks:

```text
Result<T>
Error
```

abstractions where appropriate.

HTTP conversion SHOULD use the existing Result-to-HTTP mapping functionality.

The API MUST NOT expose domain exceptions as its primary application error contract.

---

# 18. Mapping

QuizArena MUST NOT introduce AutoMapper.

Mapping MUST be explicit.

Vertical Slice Architecture SHOULD keep mappings close to the feature that consumes them.

For example:

```text
Features/Games/GetGame.cs
```

SHOULD contain the mapping necessary to transform its application result into its API response.

---

# 19. Multi-Targeting

QuizArena MUST target:

```text
net10.0
net11.0
```

in alignment with the existing BuildingBlocks.

The implementation SHOULD maintain source compatibility across both target frameworks.

Framework-specific code MUST be isolated and documented when unavoidable.

---

# 20. Architectural Dependency Rules

The expected dependency graph is:

```text
                       BuildingBlocks
                             ▲
                             │
             ┌───────────────┼────────────────┐
             │               │                │
             │               │                │
        QuizArena.Domain  Application   Infrastructure
             │               │                │
             └───────────────┼────────────────┘
                             │
                          QuizArena
                             │
                             ▼
                           API/Web
```

More specifically:

```text
Domain
 └── BuildingBlocks.Kernel.Domain

Application
 ├── QuizArena.Domain
 └── BuildingBlocks.CQRS
     BuildingBlocks.Kernel.Domain

Infrastructure
 ├── QuizArena.Domain
 ├── BuildingBlocks.Kernel.Infrastructure
 └── BuildingBlocks.EventBus.RabbitMQ

API
 ├── QuizArena.Application
 ├── QuizArena.Infrastructure
 └── BuildingBlocks.ServiceDefaults
```

---

# 21. Forbidden Architecture

The following patterns are prohibited unless explicitly approved by an ADR:

```text
Controller → EF Core DbContext → Database

Controller → Domain entity mutation

Handler → RabbitMQ direct publishing without Outbox

Domain → EF Core

Domain → ASP.NET Core

Domain → RabbitMQ

Application → Concrete repository

Duplicate CQRS dispatcher

Duplicate EventBus

Duplicate Result abstraction

Duplicate Specification framework

MediatR

MassTransit

AutoMapper
```

---

# 22. Architectural Testing

Architecture tests MUST verify that QuizArena does not violate the dependency rules.

At minimum the architecture tests SHOULD detect:

```text
Domain referencing ASP.NET Core
Domain referencing EF Core
Domain referencing RabbitMQ
Application referencing API
Application referencing concrete infrastructure
Unauthorized dependency on MediatR
Unauthorized dependency on MassTransit
Unauthorized dependency on AutoMapper
```

---

# 23. Evaluation Principle

The use of BuildingBlocks is part of the technical assessment.

The implementation should demonstrate that the developer can:

1. Understand an existing architecture.
2. Reuse existing infrastructure.
3. Extend existing abstractions correctly.
4. Avoid unnecessary duplication.
5. Implement domain behavior independently.
6. Integrate CQRS and Vertical Slice Architecture.
7. Use transactional Outbox correctly.
8. Implement idempotent integration handlers.
9. Work with multi-targeted .NET libraries.
10. Maintain architectural boundaries.

---

# 24. Final Rule

> **Do not rebuild the platform. Build the game.**

BuildingBlocks provide the technical foundation.

QuizArena provides the business domain.

The quality of the solution will be evaluated by how cleanly these two responsibilities remain separated.
