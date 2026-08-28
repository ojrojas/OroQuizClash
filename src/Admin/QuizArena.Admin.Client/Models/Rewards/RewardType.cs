namespace QuizArena.Admin.Client.Models.Rewards;

/// <summary>
/// Closed catalog of 6 reward types (Constitution C). Monetary/Physical/Digital/Voucher/Experience/Consolation.
/// Consolation is independent (Constitution C) — not redeemable as normal reward.
/// </summary>
public enum RewardType
{
    Monetary = 0,
    Physical = 1,
    Digital = 2,
    Voucher = 3,
    Experience = 4,
    Consolation = 5
}

public static class RewardTypeMap
{
    public static RewardType FromApi(string? apiValue) => apiValue?.ToUpperInvariant() switch
    {
        "MONETARY" => RewardType.Monetary,
        "PHYSICAL" => RewardType.Physical,
        "DIGITAL" => RewardType.Digital,
        "VOUCHER" => RewardType.Voucher,
        "EXPERIENCE" => RewardType.Experience,
        "CONSOLATION" => RewardType.Consolation,
        _ => RewardType.Physical
    };

    public static string ToApi(RewardType type) => type switch
    {
        RewardType.Monetary => "MONETARY",
        RewardType.Physical => "PHYSICAL",
        RewardType.Digital => "DIGITAL",
        RewardType.Voucher => "VOUCHER",
        RewardType.Experience => "EXPERIENCE",
        RewardType.Consolation => "CONSOLATION",
        _ => "PHYSICAL"
    };

    public static string DisplayName(RewardType type) => type switch
    {
        RewardType.Monetary => "Monetario",
        RewardType.Physical => "Físico",
        RewardType.Digital => "Digital",
        RewardType.Voucher => "Vale",
        RewardType.Experience => "Experiencia",
        RewardType.Consolation => "Consolación",
        _ => type.ToString()
    };

    public static bool IsConsolation(RewardType type) => type == RewardType.Consolation;

    /// <summary>
    /// Stock 0 = unlimited for Digital/Voucher/Experience/Consolation per R3;
    /// limited for Physical/Monetary. UI shows tooltip accordingly.
    /// </summary>
    public static bool IsStockUnlimitedAllowed(RewardType type) =>
        type is RewardType.Digital or RewardType.Voucher or RewardType.Experience or RewardType.Consolation;
}
