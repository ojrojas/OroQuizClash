namespace QuizArena.Admin.Client.Models.Audit;

public sealed record WhoView(
    string ActorId,
    string DisplayName,
    string Email,
    string? TenantId);

public sealed record WhereView(
    string Service,
    string Endpoint,
    string? IpAddress,
    string CorrelationId,
    string? TraceId);

public sealed record EntityView(
    string EntityType,
    Guid EntityId);

public sealed record ResultView(
    string Status,
    string? ErrorCode,
    string? Detail);

public sealed record AuditEntry(
    Guid AuditId,
    WhoView Who,
    string What,
    DateTimeOffset When,
    WhereView Where,
    EntityView Entity,
    string? PreviousValue,
    string? NewValue,
    string Action,
    ResultView Result);
