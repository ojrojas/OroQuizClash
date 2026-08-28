using QuizArena.Admin.Client.Models.Rewards;

namespace QuizArena.Admin.Client.Services;

public static class RewardCatalogs
{
    public static readonly IReadOnlyList<string> Types =
        ["Monetary", "Physical", "Digital", "Voucher", "Experience", "Consolation"];

    public static readonly IReadOnlyList<string> Statuses =
        ["Active", "Inactive", "Archived"];

    public static readonly IReadOnlyList<string> RedemptionStatuses =
        ["Requested", "Approved", "Rejected", "Delivered", "Cancelled"];

    public const int CostMin = 1;
    public const int CostMax = 100000;
    public const int StockMin = 0;

    public static IReadOnlyList<(string Value, string Label)> TypeOptions =>
        Enum.GetValues<RewardType>()
            .Select(t => (RewardTypeMap.ToApi(t), RewardTypeMap.DisplayName(t)))
            .ToList();

    public static IReadOnlyList<(string Value, string Label)> StatusOptions =>
        Enum.GetValues<RewardStateView>()
            .Select(s => (RewardStateMap.ToApi(s), RewardStateMap.DisplayName(s)))
            .ToList();

    public static IReadOnlyList<(string Value, string Label)> RedemptionStatusOptions =>
        Enum.GetValues<RedemptionStateView>()
            .Select(s => (RedemptionStateMap.ToApi(s), RedemptionStateMap.DisplayName(s)))
            .ToList();

    public static bool IsValidType(string? type) =>
        type is not null && Types.Contains(type, StringComparer.OrdinalIgnoreCase);

    public static bool IsValidCost(int cost) => cost is >= CostMin and <= CostMax;

    public static bool IsValidStock(int stock) => stock >= StockMin;

    public static bool IsValidAvailability(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from.HasValue && to.HasValue) return from.Value < to.Value;
        return true;
    }
}
