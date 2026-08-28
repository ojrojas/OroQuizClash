using System.Text.Json.Serialization;

namespace QuizArena.Admin.Client.Models.Dashboard;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MetricId
{
    ActiveGames,
    ScheduledGames,
    FinishedGames,
    ConnectedPlayers,
    ActivePlayers,
    AvailableQuestions,
    Categories,
    Rewards,
    Redemptions,
    GeneralStatistics
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MetricState
{
    Loading,
    Ready,
    Empty,
    Error
}

public sealed record MetricValue(
    MetricId Id,
    string Label,
    int Count,
    MetricState State,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    string? SourceLabel = null,
    string? Tooltip = null,
    bool Retryable = false,
    string? DrillDownRoute = null);

public sealed record DashboardSnapshot(
    DateTimeOffset GeneratedAt,
    string? CorrelationId,
    IReadOnlyList<MetricValue> Metrics,
    GeneralStatistics Statistics);

public sealed record GeneralStatistics(
    int TotalGames,
    int TotalParticipations,
    double AvgQuestionsPerCategory,
    IReadOnlyList<StatisticBreakdown>? Breakdown = null);

public sealed record StatisticBreakdown(string Key, string Label, string Value);

public sealed record DashboardViewState(
    DashboardSnapshot? Snapshot,
    bool IsRefreshing,
    bool AutoRefreshEnabled,
    DateTimeOffset? LastRefreshAt,
    bool SessionExpired);
