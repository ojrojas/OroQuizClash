using BuildingBlocks.Kernel.Domain.Entities;

namespace OroQuizClash.Domain.Audit;

public sealed class AuditEntry : AggregateRoot<Guid>
{
    public DateTimeOffset Timestamp { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public string ActorRoles { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string Permission { get; private set; } = string.Empty;
    public string Resource { get; private set; } = string.Empty;
    public string? ResourceId { get; private set; }
    public Guid? GameId { get; private set; }
    public Guid? PlayerId { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string? TenantId { get; private set; }
    public string Result { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? Details { get; private set; }
    public string? Data { get; private set; }

    private AuditEntry() { }

    private AuditEntry(Guid id, DateTimeOffset timestamp, string actorId, string actorRoles, string action, string permission, string resource, string? resourceId, Guid? gameId, Guid? playerId, string correlationId, string? tenantId, string result, string? reason, string? details, string? data) : base(id)
    {
        Timestamp = timestamp;
        ActorId = actorId;
        ActorRoles = actorRoles;
        Action = action;
        Permission = permission;
        Resource = resource;
        ResourceId = resourceId;
        GameId = gameId;
        PlayerId = playerId;
        CorrelationId = correlationId;
        TenantId = tenantId;
        Result = result;
        Reason = reason;
        Details = details;
        Data = data ?? details;
    }

    public static AuditEntry Create(DateTimeOffset timestamp, string actorId, string actorRoles, string action, string permission, string resource, string correlationId, string? tenantId, string result, string? reason, string? details)
    {
        return new AuditEntry(Guid.NewGuid(), timestamp, actorId, actorRoles, action, permission, resource, null, null, null, correlationId, tenantId, result, reason, details, details);
    }

    public static AuditEntry Create(DateTimeOffset timestamp, string actorId, string actorRoles, string action, string permission, string resource, string? resourceId, Guid? gameId, Guid? playerId, string correlationId, string? tenantId, string result, string? reason, string? data)
    {
        return new AuditEntry(Guid.NewGuid(), timestamp, actorId, actorRoles, action, permission, resource, resourceId, gameId, playerId, correlationId, tenantId, result, reason, data, data);
    }
}
