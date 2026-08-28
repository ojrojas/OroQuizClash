namespace QuizArena.Admin.Client.Models.Rewards;

public enum RewardStateView
{
    Active = 0,
    Inactive = 1,
    Archived = 2
}

public static class RewardStateMap
{
    public static RewardStateView FromApi(string? apiStatus) => apiStatus?.ToUpperInvariant() switch
    {
        "ACTIVE" => RewardStateView.Active,
        "INACTIVE" => RewardStateView.Inactive,
        "ARCHIVED" => RewardStateView.Archived,
        _ => RewardStateView.Inactive
    };

    public static string ToApi(RewardStateView status) => status switch
    {
        RewardStateView.Active => "ACTIVE",
        RewardStateView.Inactive => "INACTIVE",
        RewardStateView.Archived => "ARCHIVED",
        _ => "INACTIVE"
    };

    public static string DisplayName(RewardStateView status) => status switch
    {
        RewardStateView.Active => "Activo",
        RewardStateView.Inactive => "Inactivo",
        RewardStateView.Archived => "Archivado",
        _ => status.ToString()
    };

    public static bool IsTerminal(RewardStateView status) => status == RewardStateView.Archived;
    public static bool CanEdit(RewardStateView status) => status != RewardStateView.Archived;
}

public sealed record Reward(
    Guid RewardId,
    string Name,
    string? Description,
    RewardType Type,
    int Cost,
    int Stock,
    DateTimeOffset? AvailableFrom,
    DateTimeOffset? AvailableTo,
    RewardStateView Status,
    bool IsEligible,
    string RowVersion);

public sealed record RewardSummary(
    Guid Id,
    string Name,
    RewardType Type,
    int Cost,
    int Stock,
    RewardStateView Status,
    bool IsEligible,
    string RowVersion);

public sealed record RewardDetail(
    Guid Id,
    string Name,
    string? Description,
    RewardType Type,
    int Cost,
    int Stock,
    DateTimeOffset? AvailableFrom,
    DateTimeOffset? AvailableTo,
    RewardStateView Status,
    bool IsEligible,
    string RowVersion,
    IReadOnlyList<RewardStateTransition> History)
{
    public static bool ComputeIsEligible(RewardStateView status, int stock, RewardType type, DateTimeOffset? from, DateTimeOffset? to, DateTimeOffset now)
    {
        if (status != RewardStateView.Active) return false;
        var hasStock = stock == 0 ? RewardTypeMap.IsStockUnlimitedAllowed(type) : stock > 0;
        if (!hasStock) return false;
        if (from.HasValue && now < from.Value) return false;
        if (to.HasValue && now > to.Value) return false;
        if (from.HasValue && to.HasValue && from.Value >= to.Value) return false;
        return true;
    }
}

public sealed record RewardStateTransition(
    RewardStateView From,
    RewardStateView To,
    DateTimeOffset Timestamp,
    string ActorId,
    string? Reason);

public sealed record RewardAuditEntry(
    Guid RewardId,
    string ActorId,
    DateTimeOffset Timestamp,
    RewardStateView FromState,
    RewardStateView ToState,
    string Action,
    string? Reason,
    string CorrelationId,
    string Result,
    string IdempotencyKey);

public sealed record RewardFilter(
    RewardType? Type = null,
    RewardStateView? Status = null,
    string? Search = null,
    bool? OnlyEligible = null,
    int Page = 1,
    int PageSize = 20);

public sealed record PagedRewardResult(IReadOnlyList<RewardSummary> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
