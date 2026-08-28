namespace QuizArena.Admin.Client.Models.GameConfiguration;

public enum DifficultyStrategy
{
    Linear,
    Progressive,
    Adaptive,
    CategorySpecific
}

public enum WithdrawalPolicy
{
    LoseAll,
    KeepCurrentScore,
    KeepSecuredScore,
    KeepCheckpointScore
}

public enum LossPolicy
{
    LoseAll,
    LoseCurrentRound,
    LoseUnsecuredPoints,
    FallbackToCheckpoint
}

public enum ScoringSystem
{
    Standard,
    ProgressiveBonus
}

public enum SecuredPointsPolicy
{
    None,
    KeepCheckpoint,
    KeepSecured
}

public static class PolicyCatalogs
{
    public static string ToApi(DifficultyStrategy v) => v switch
    {
        DifficultyStrategy.Linear => "Linear",
        DifficultyStrategy.Progressive => "Progressive",
        DifficultyStrategy.Adaptive => "Adaptive",
        DifficultyStrategy.CategorySpecific => "CategorySpecific",
        _ => "Linear"
    };

    public static string ToApi(WithdrawalPolicy v) => v switch
    {
        WithdrawalPolicy.LoseAll => "LOSE_ALL",
        WithdrawalPolicy.KeepCurrentScore => "KEEP_CURRENT_SCORE",
        WithdrawalPolicy.KeepSecuredScore => "KEEP_SECURED_SCORE",
        WithdrawalPolicy.KeepCheckpointScore => "KEEP_CHECKPOINT_SCORE",
        _ => "LOSE_ALL"
    };

    public static string ToApi(LossPolicy v) => v switch
    {
        LossPolicy.LoseAll => "LOSE_ALL",
        LossPolicy.LoseCurrentRound => "LOSE_CURRENT_ROUND",
        LossPolicy.LoseUnsecuredPoints => "LOSE_UNSECURED_POINTS",
        LossPolicy.FallbackToCheckpoint => "FALLBACK_TO_CHECKPOINT",
        _ => "LOSE_ALL"
    };

    public static string ToApi(ScoringSystem v) => v switch
    {
        ScoringSystem.Standard => "Standard",
        ScoringSystem.ProgressiveBonus => "ProgressiveBonus",
        _ => "Standard"
    };

    public static string DisplayName(DifficultyStrategy v) => v.ToString();
    public static string DisplayName(WithdrawalPolicy v) => v.ToString();
    public static string DisplayName(LossPolicy v) => v.ToString();
    public static string DisplayName(ScoringSystem v) => v.ToString();
    public static string DisplayName(SecuredPointsPolicy v) => v.ToString();
}
