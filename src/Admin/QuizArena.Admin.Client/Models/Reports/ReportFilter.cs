using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Client.Models.Reports;

public sealed record ReportFilter(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? CategoryId = null,
    string? CategoryName = null,
    Guid? GameId = null,
    string? GameName = null,
    Guid? PlayerId = null,
    string? PlayerSearch = null,
    int? Level = null,
    string? Result = null,
    int Page = 1,
    int PageSize = 20)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (From.HasValue && To.HasValue && From.Value > To.Value)
            errors["DateRange"] = ["Desde debe ser ≤ Hasta."];
        if (Level is < 1 or > 5)
            errors[nameof(Level)] = ["Nivel debe estar entre 1 y 5."];
        if (Result is not null && !ReportCatalogs.IsValidResult(Result))
            errors[nameof(Result)] = ["Resultado no válido."];
        if (Page < 1)
            errors[nameof(Page)] = ["Page debe ser ≥1."];
        if (PageSize is < 1 or > 100)
            errors[nameof(PageSize)] = ["PageSize debe estar entre 1 y 100."];
        return errors;
    }

    public bool IsValid => Validate().Count == 0;
}
