namespace QuizArena.Admin.Client.Models.Audit;

public sealed record JsonDiffEntry(
    string Path,
    string? Previous,
    string? New,
    string ChangeType);

public sealed record AuditDetail(
    Guid AuditId,
    WhoView Who,
    string What,
    DateTimeOffset When,
    WhereView Where,
    EntityView Entity,
    string? PreviousValue,
    string? NewValue,
    string Action,
    ResultView Result,
    IReadOnlyList<JsonDiffEntry> Diff);

public sealed record AuditViewAudit(
    Guid ViewId,
    string ActorId,
    AuditFilter Filters,
    DateTimeOffset Timestamp,
    string CorrelationId);
