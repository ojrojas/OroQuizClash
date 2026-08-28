namespace QuizArena.Admin.Client.Models;

public sealed record PlayerStatusView(
    Guid PlayerId,
    string? DisplayName,
    Guid GameId,
    string State,
    int CurrentPoints,
    int SecuredPoints,
    DateTimeOffset? ExitedAt);

public sealed record ConsolationHistoryEntry(
    Guid GameId,
    string GameName,
    string Policy,
    int? Points,
    string? RewardName,
    DateTimeOffset Timestamp);

public enum RewardStatusView
{
    Active,
    Inactive
}

public static class RewardStatusMap
{
    public static RewardStatusView FromApi(string? apiStatus) => apiStatus?.ToUpperInvariant() switch
    {
        "ACTIVE" => RewardStatusView.Active,
        _ => RewardStatusView.Inactive
    };

    public static string ToApi(RewardStatusView status) => status == RewardStatusView.Active ? "ACTIVE" : "INACTIVE";
}

public sealed record RewardSummary(
    Guid Id,
    string Name,
    string Description,
    int PointCost,
    int Stock,
    RewardStatusView Status,
    DateTimeOffset? ExpirationDate,
    bool Available);

public sealed record RewardForm(
    string Name,
    string Description,
    int PointCost,
    int? Stock,
    DateTimeOffset? ExpirationDate = null)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length is < 3 or > 100)
            errors[nameof(Name)] = ["Name must be 3-100 characters."];
        if (PointCost <= 0)
            errors[nameof(PointCost)] = ["Point cost must be greater than zero."];
        if (Stock is < 0)
            errors[nameof(Stock)] = ["Stock must be zero or positive."];
        return errors;
    }

    public bool IsValid => Validate().Count == 0;
}

public enum RedemptionStatusView
{
    Pending,
    Approved,
    Rejected,
    Delivered,
    Cancelled
}

public static class RedemptionStatusMap
{
    public static RedemptionStatusView FromApi(string? apiStatus) => apiStatus?.ToUpperInvariant() switch
    {
        "APPROVED" => RedemptionStatusView.Approved,
        "REJECTED" => RedemptionStatusView.Rejected,
        "DELIVERED" => RedemptionStatusView.Delivered,
        "CANCELLED" => RedemptionStatusView.Cancelled,
        _ => RedemptionStatusView.Pending
    };

    public static string ToApi(RedemptionStatusView status) => status switch
    {
        RedemptionStatusView.Approved => "APPROVED",
        RedemptionStatusView.Rejected => "REJECTED",
        RedemptionStatusView.Delivered => "DELIVERED",
        RedemptionStatusView.Cancelled => "CANCELLED",
        _ => "REQUESTED"
    };

    public static bool IsTerminal(RedemptionStatusView status) =>
        status is RedemptionStatusView.Rejected or RedemptionStatusView.Delivered or RedemptionStatusView.Cancelled;
}

public sealed record RedemptionSummary(
    Guid Id,
    Guid PlayerId,
    Guid RewardId,
    Guid GameId,
    int PointCost,
    RedemptionStatusView Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? DecidedAt,
    string? PlayerName,
    string? RewardName);
