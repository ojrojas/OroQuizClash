namespace QuizArena.Admin.Client.Models.Reports;

public sealed record PerformanceMetrics(
    AnswerMetric Answers,
    ScoreMetric Scores,
    WithdrawalMetric Withdrawals);

public sealed record AnswerMetric(
    int TotalAnswers,
    int CorrectAnswers,
    int IncorrectAnswers,
    double AccuracyRate);

public sealed record ScoreMetric(
    int TotalPoints,
    double AverageScore,
    IReadOnlyDictionary<string, int> Distribution,
    IReadOnlyDictionary<string, int> ByTransactionType);

public sealed record WithdrawalMetric(
    int TotalWithdrawals,
    IReadOnlyDictionary<string, int> ByPolicy,
    double Rate);

public sealed record RewardsMetrics(
    RewardMetric Rewards,
    RedemptionMetric Redemptions,
    ConsolationMetric Consolations);

public sealed record RewardMetric(
    int TotalRewards,
    IReadOnlyDictionary<string, int> ByType,
    IReadOnlyDictionary<string, int> ByStatus);

public sealed record RedemptionMetric(
    int TotalRedemptions,
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyDictionary<string, int> ByType,
    int TotalCost);

public sealed record ConsolationMetric(
    int TotalConsolations,
    int TotalCostConsolation,
    IReadOnlyDictionary<string, int> ByEligibility);
