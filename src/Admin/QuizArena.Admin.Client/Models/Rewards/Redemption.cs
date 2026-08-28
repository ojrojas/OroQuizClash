namespace QuizArena.Admin.Client.Models.Rewards;

public enum RedemptionStateView
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
    Delivered = 3,
    Cancelled = 4
}

public static class RedemptionStateMap
{
    public static RedemptionStateView FromApi(string? apiStatus) => apiStatus?.ToUpperInvariant() switch
    {
        "REQUESTED" or "PENDING" => RedemptionStateView.Requested,
        "APPROVED" => RedemptionStateView.Approved,
        "REJECTED" => RedemptionStateView.Rejected,
        "DELIVERED" => RedemptionStateView.Delivered,
        "CANCELLED" => RedemptionStateView.Cancelled,
        _ => RedemptionStateView.Requested
    };

    public static string ToApi(RedemptionStateView status) => status switch
    {
        RedemptionStateView.Requested => "REQUESTED",
        RedemptionStateView.Approved => "APPROVED",
        RedemptionStateView.Rejected => "REJECTED",
        RedemptionStateView.Delivered => "DELIVERED",
        RedemptionStateView.Cancelled => "CANCELLED",
        _ => "REQUESTED"
    };

    public static string DisplayName(RedemptionStateView status) => status switch
    {
        RedemptionStateView.Requested => "Solicitado",
        RedemptionStateView.Approved => "Aprobado",
        RedemptionStateView.Rejected => "Rechazado",
        RedemptionStateView.Delivered => "Entregado",
        RedemptionStateView.Cancelled => "Cancelado",
        _ => status.ToString()
    };

    public static bool IsTerminal(RedemptionStateView status) =>
        status is RedemptionStateView.Rejected or RedemptionStateView.Delivered or RedemptionStateView.Cancelled;

    public static bool IsValidTransition(RedemptionStateView from, RedemptionStateView to) => (from, to) switch
    {
        (RedemptionStateView.Requested, RedemptionStateView.Approved) => true,
        (RedemptionStateView.Requested, RedemptionStateView.Rejected) => true,
        (RedemptionStateView.Requested, RedemptionStateView.Cancelled) => true,
        (RedemptionStateView.Approved, RedemptionStateView.Delivered) => true,
        (RedemptionStateView.Approved, RedemptionStateView.Cancelled) => true,
        _ => false
    };
}

public sealed record RewardRedemption(
    Guid RedemptionId,
    Guid RewardId,
    string RewardName,
    RewardType RewardType,
    Guid PlayerId,
    string PlayerName,
    int Cost,
    RedemptionStateView Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? RejectedAt,
    DateTimeOffset? DeliveredAt,
    string? Reason,
    bool IsConsolation,
    string RowVersion);

public sealed record RedemptionStateTransition(
    RedemptionStateView From,
    RedemptionStateView To,
    DateTimeOffset Timestamp,
    string ActorId,
    string? Reason);

public sealed record RedemptionFilter(
    RedemptionStateView? Status = null,
    RewardType? Type = null,
    Guid? PlayerId = null,
    string? Search = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20);

public sealed record StockInfo(int Stock, bool IsUnlimited)
{
    public static StockInfo From(int stock, RewardType type) =>
        new(stock, stock == 0 && RewardTypeMap.IsStockUnlimitedAllowed(type));
}

public sealed record AvailabilityInfo(DateTimeOffset? From, DateTimeOffset? To, bool IsEligible)
{
    public static bool IsInWindow(DateTimeOffset? from, DateTimeOffset? to, DateTimeOffset now)
    {
        if (from.HasValue && now < from.Value) return false;
        if (to.HasValue && now > to.Value) return false;
        return true;
    }
}
