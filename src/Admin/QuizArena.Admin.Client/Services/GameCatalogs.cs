using QuizArena.Admin.Client.Models.GameConfiguration;

namespace QuizArena.Admin.Client.Services;

public static class GameCatalogs
{
    public static readonly IReadOnlyList<(string Value, string Label)> DifficultyStrategies =
    [
        ("Linear", "Linear"),
        ("Progressive", "Progressive"),
        ("Adaptive", "Adaptive"),
        ("CategorySpecific", "CategorySpecific")
    ];

    public static readonly IReadOnlyList<(string Value, string Label)> WithdrawalPolicies =
    [
        ("LOSE_ALL", "Lose All"),
        ("KEEP_CURRENT_SCORE", "Keep Current Score"),
        ("KEEP_SECURED_SCORE", "Keep Secured Score"),
        ("KEEP_CHECKPOINT_SCORE", "Keep Checkpoint Score")
    ];

    public static readonly IReadOnlyList<(string Value, string Label)> LossPolicies =
    [
        ("LOSE_ALL", "Lose All"),
        ("LOSE_CURRENT_ROUND", "Lose Current Round"),
        ("LOSE_UNSECURED_POINTS", "Lose Unsecured Points"),
        ("FALLBACK_TO_CHECKPOINT", "Fallback to Checkpoint")
    ];

    public static readonly IReadOnlyList<(string Value, string Label)> ScoringSystems =
    [
        ("Standard", "Standard"),
        ("ProgressiveBonus", "Progressive Bonus")
    ];

    public static readonly IReadOnlyList<int> Difficulties = [1, 2, 3, 4, 5];
    public static readonly IReadOnlyList<int> Rounds = [5, 6, 7, 8, 9, 10];
}
