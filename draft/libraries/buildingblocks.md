# BuildingBlocks

Librerías base para implantar **DDD + Vertical Slice + CQRS + EventBus (RabbitMQ)** en .NET, con multi-targeting `net10.0`.

Sin dependencias de AutoMapper, MediatR ni MassTransit: el dispatcher CQRS y el bus de eventos están implementados sobre `Microsoft.Extensions.*` y el cliente oficial `RabbitMQ.Client`.

## Proyectos

| Proyecto | Capa | Contenido |
|---|---|---|
| `BuildingBlocks.Kernel.Domain` | Dominio (Core) | `Entity`, `AggregateRoot`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `IDomainEvent`, `IBusinessRule`, `Result`/`Error`, `IRepository`, `IUnitOfWork`, `Specification<T>` componible (`And`/`Or`/`Not`) |
| `BuildingBlocks.CQRS` | Aplicación | `ICommand`/`IQuery`/handlers, `ISender` (dispatcher propio), `IPipelineBehavior` (Logging, Validation), `IDomainEventHandler` + dispatcher, validación ligera propia |
| `BuildingBlocks.EventBus` | Aplicación/Contratos | `IntegrationEvent`, `IEventBus`, `IIntegrationEventHandler`, gestor de suscripciones |
| `BuildingBlocks.EventBus.RabbitMQ` | Infraestructura | Bus sobre exchange topic durable con publisher confirms, consumer `BackgroundService` con ack manual y reintentos exponenciales |
| `BuildingBlocks.Kernel.Infrastructure` | Infraestructura | `AppDbContextBase` (EF Core, despacho de domain events en `SaveChanges`), `EfRepository` con soporte de specifications (`SpecificationEvaluator`), Outbox transaccional (`IOutboxWriter` + `OutboxProcessor`) |
| `BuildingBlocks.ServiceDefaults` | Host | OpenTelemetry (logs/traces/metrics + OTLP), health checks (`/health`, `/alive`), resiliencia HTTP estándar, `IEndpoint` para Vertical Slice, `Result → HTTP` y `GlobalExceptionHandler` |

## Uso rápido

### Program.cs de un microservicio

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(); // OTel + health checks + HttpClient resiliente

builder.Services.AddCqrs(cqrs => cqrs
    .RegisterHandlersFromAssemblyContaining<Program>()
    .AddOpenBehavior(typeof(LoggingBehavior<,>))
    .AddOpenBehavior(typeof(ValidationBehavior<,>)));

builder.Services.AddDbContext<OrdersDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddUnitOfWork<OrdersDbContext>();
builder.Services.AddOutbox<OrdersDbContext>();

builder.Services
    .AddRabbitMqEventBus(builder.Configuration)
    .AddSubscription<OrderCreatedIntegrationEvent, OrderCreatedHandler>();

builder.Services.AddEndpoints(typeof(Program).Assembly);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapEndpoints();
app.Run();
```

### appsettings.json

```json
{
  "EventBus": {
    "RabbitMq": {
      "HostName": "localhost",
      "UserName": "guest",
      "Password": "guest",
      "ExchangeName": "integration_events",
      "QueueName": "orders-service"
    }
  }
}
```

### Un vertical slice completo (Features/Orders/CreateOrder.cs)

```csharp
// 1. Command + resultado
public sealed record CreateOrderCommand(Guid CustomerId, decimal Amount) : ICommand<Result<Guid>>;

// 2. Validador (opcional, corre en el ValidationBehavior)
public sealed class CreateOrderValidator : Validator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId != Guid.Empty, nameof(CreateOrderCommand.CustomerId), "CustomerId es obligatorio.");
        RuleFor(x => x.Amount > 0, nameof(CreateOrderCommand.Amount), "Amount debe ser mayor que 0.");
    }
}

// 3. Handler
public sealed class CreateOrderHandler(
    IRepository<Order, OrderId> orders,
    IOutboxWriter outbox,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(CreateOrderCommand command, CancellationToken ct)
    {
        var order = Order.Create(command.CustomerId, command.Amount); // levanta OrderCreatedDomainEvent
        await orders.AddAsync(order, ct);
        await outbox.StageAsync(new OrderCreatedIntegrationEvent(order.Id.Value), ct);
        await unitOfWork.SaveChangesAsync(ct); // domain events + outbox en la misma transacción
        return order.Id.Value;
    }
}

// 4. Endpoint del slice
public sealed class CreateOrderEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/orders", async (CreateOrderCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(command, ct);
            return result.ToCreatedResult(id => $"/orders/{id}");
        });
}
```

### Specifications (consultas del dominio)

Las consultas se expresan como specifications en el dominio y el `EfRepository` las traduce a SQL:

```csharp
public sealed class UserByEmailSpecification : Specification<User>
{
    public UserByEmailSpecification(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        Where(user => user.Email == normalized);
    }
}

// Composición: login por username O email
var spec = new UserByEmailSpecification(login).Or(new UserByUserNameSpecification(login));
var user = await users.FirstOrDefaultAsync(spec, ct);

// También: ListAsync, AnyAsync, CountAsync; e IsSatisfiedBy(entity) en memoria para tests
```

`Specification<T>` soporta `Where` (acumulativo con AND), `And`/`Or`/`Not`, `AddInclude`,
ordenación, paginación y `ApplyAsNoTracking()` para lecturas.

### Agregado con eventos de dominio

```csharp
public sealed record OrderId(Guid Value) : StronglyTypedId<Guid>(Value);

public sealed class Order : AggregateRoot<OrderId>
{
    public static Order Create(Guid customerId, decimal amount)
    {
        CheckRule(new AmountMustBePositiveRule(amount));
        var order = new Order(new OrderId(Guid.NewGuid()), customerId, amount);
        order.RaiseDomainEvent(new OrderCreatedDomainEvent(order.Id));
        return order;
    }
    // ...
}
```

### Outbox

El `DbContext` del servicio debe heredar de `AppDbContextBase` e incluir la entidad del outbox:

```csharp
public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options, IDomainEventDispatcher dispatcher)
    : AppDbContextBase(options, dispatcher)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OutboxEntityTypeConfiguration());
        // ... resto del modelo
    }
}
```

## Decisiones de diseño

- **Sin MediatR**: `Sender` resuelve handlers desde DI con wrappers genéricos cacheados; los behaviors son open generics registrados en orden.
- **Sin MassTransit**: `RabbitMqEventBus` publica a un exchange *topic* durable con publisher confirms; cada servicio consume desde su propia cola (`QueueName`) con ack manual y QoS configurable. Entrega *at-least-once*: los handlers de integración deben ser idempotentes.
- **Sin AutoMapper**: mapea a mano en los handlers (en vertical slices el mapping es local a cada feature).
- **Domain events vs integration events**: los primeros son in-process y se despachan dentro de `SaveChanges`; los segundos cruzan servicios y salen por el outbox transaccional para no perder mensajes.

## Ejemplo completo

En [examples/Identity](examples/Identity/README.md) hay un servidor de identidad OAuth2/OIDC con
**OpenIddict 7 + Blazor (InteractiveAuto + WASM)** que ejercita todas las librerías: agregado `User`
con domain events y reglas, vertical slices con `ISender`, outbox → EventBus, y ServiceDefaults.

## Compilar

```
dotnet build BuildingBlocks.slnx
```

Requiere SDK de .NET 10