namespace QuizArena.Admin.Client.Models;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public sealed record ApiErrorView(
    string Code,
    string Title,
    string? Detail = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static ApiErrorView Unknown { get; } = new("unknown", "Unexpected error", "The operation could not be completed. Try again.");
}

public sealed class ApiErrorException(ApiErrorView error) : Exception(error.Title)
{
    public ApiErrorView ErrorView { get; } = error;
}

public sealed record DateRange(DateTimeOffset? From, DateTimeOffset? To);

public sealed record GameFilter(
    GameStatusView? Status = null,
    Guid? CategoryId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

public sealed record CategoryFilter(
    string? KnowledgeArea = null,
    string? AcademicLevel = null,
    int? Difficulty = null,
    CategoryStatusView? Status = null,
    string? Tag = null,
    int Page = 1,
    int PageSize = 20);

public sealed record QuestionFilter(
    Guid? CategoryId = null,
    int? Difficulty = null,
    QuestionStatusView? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

public sealed record RedemptionFilter(
    RedemptionStatusView? Status = null,
    int Page = 1,
    int PageSize = 20);

public sealed record AuditFilter(
    string? ActorId = null,
    string? Action = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20);
