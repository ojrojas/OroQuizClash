using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Client.Models.Audit;

public sealed record AuditFilter(
    string? Who = null,
    string? What = null,
    DateTimeOffset? WhenFrom = null,
    DateTimeOffset? WhenTo = null,
    string? Where = null,
    string? EntityType = null,
    Guid? EntityId = null,
    string? Action = null,
    string? Result = null,
    string? ErrorCode = null,
    int Page = 1,
    int PageSize = 20)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (WhenFrom.HasValue && WhenTo.HasValue && WhenFrom.Value > WhenTo.Value)
            errors["DateRange"] = ["Desde debe ser ≤ Hasta."];
        if (Action is not null && !AuditCatalogs.IsValidAction(Action))
            errors[nameof(Action)] = ["Action no válido."];
        if (Result is not null && !AuditCatalogs.IsValidResult(Result))
            errors[nameof(Result)] = ["Result no válido."];
        if (EntityType is not null && !AuditCatalogs.IsValidEntityType(EntityType))
            errors[nameof(EntityType)] = ["EntityType no válido."];
        if (Page < 1)
            errors[nameof(Page)] = ["Page debe ser ≥1."];
        if (PageSize is < 1 or > 100)
            errors[nameof(PageSize)] = ["PageSize debe estar entre 1 y 100."];
        return errors;
    }

    public bool IsValid => Validate().Count == 0;
}
