using BuildingBlocks.Kernel.Domain.Specifications;

using OroQuizClash.Domain.Audit;

namespace OroQuizClash.Infrastructure.Specifications;

public sealed class AuditEntrySpecification : Specification<AuditEntry>
{
    public AuditEntrySpecification(
        string? correlationId = null,
        string? actorId = null,
        string? action = null,
        string? resource = null,
        string? result = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 20)
    {
        if (!string.IsNullOrWhiteSpace(correlationId)) Where(e => e.CorrelationId == correlationId);
        if (!string.IsNullOrWhiteSpace(actorId)) Where(e => e.ActorId == actorId);
        if (!string.IsNullOrWhiteSpace(action)) Where(e => e.Action == action);
        if (!string.IsNullOrWhiteSpace(resource)) Where(e => e.Resource.Contains(resource));
        if (!string.IsNullOrWhiteSpace(result)) Where(e => e.Result == result);
        if (from.HasValue) Where(e => e.Timestamp >= from.Value);
        if (to.HasValue) Where(e => e.Timestamp <= to.Value);

        ApplyOrderByDescending(e => e.Timestamp);
        ApplyPaging((page - 1) * pageSize, pageSize);
    }
}
