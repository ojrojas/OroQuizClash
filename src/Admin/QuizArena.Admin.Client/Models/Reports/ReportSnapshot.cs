namespace QuizArena.Admin.Client.Models.Reports;

public sealed record ReportSnapshot(
    ReportFilter Filters,
    OperationalMetrics Operational,
    PerformanceMetrics Performance,
    RewardsMetrics Rewards,
    int TotalCount,
    DateTimeOffset CalculatedAt);

public sealed record OperationalMetrics(
    GameMetric Games,
    PlayerMetric Players,
    QuestionMetric Questions,
    CategoryMetric Categories);

public sealed record GameMetric(
    int TotalGames,
    IReadOnlyDictionary<string, int> ByStatus);

public sealed record PlayerMetric(
    int UniquePlayers,
    int ActivePlayers,
    IReadOnlyDictionary<string, int> DistributionByTenant);

public sealed record QuestionMetric(
    int TotalQuestions,
    IReadOnlyDictionary<string, int> ByCategory,
    IReadOnlyDictionary<int, int> ByLevel);

public sealed record CategoryMetric(
    int TotalCategories,
    int CategoriesInUse,
    IReadOnlyDictionary<string, int> QuestionsPerCategory);
