using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;
using ReportsModels = QuizArena.Admin.Client.Models.Reports;

namespace QuizArena.Admin.Client.Services;

/// <summary>
/// Maps the six backend report endpoints into the generic tabular <see cref="ReportResult"/>
/// (data-model §1 Reports). The API returns strongly-typed report payloads; the admin UI
/// renders them through one generic table.
/// </summary>
public class ReportsServiceCore(HttpClient http, string prefix) : IReportsService
{
    private sealed record ApiGameReportPlayer(Guid PlayerId, string? DisplayName, string Status);
    private sealed record ApiGameReportRound(Guid RoundId, int RoundNumber, Guid QuestionId);
    private sealed record ApiGameReportWinner(Guid PlayerId, string? DisplayName);
    private sealed record ApiGameReport(
        Guid GameId, string Name, DateTimeOffset Start, DateTimeOffset? End,
        IReadOnlyList<ApiGameReportPlayer> Players, IReadOnlyList<ApiGameReportRound> Rounds,
        ApiGameReportWinner? Winner, int TotalQuestions);

    private sealed record ApiCategoryReport(
        Guid CategoryId, string CategoryName, int Questions, int Games, int Players,
        double? AverageScore, double? AverageAccuracy);

    private sealed record ApiQuestionReport(
        Guid QuestionId, Guid CategoryId, string CategoryName, string Difficulty,
        int TimesPresented, int CorrectAnswers, int IncorrectAnswers,
        double? Accuracy, double? AverageResponseTime);

    private sealed record ApiPlayerReport(
        Guid PlayerId, int GamesPlayed, int GamesWon, int GamesLost, int GamesWithdrawn,
        int QuestionsAnswered, int CorrectAnswers, double? Accuracy,
        int PointsEarned, int PointsRedeemed);

    private sealed record ApiRewardReportItem(
        Guid RewardId, string RewardName, int AvailableStock, int Redemptions,
        int PointsConsumed, int Pending, int Delivered);
    private sealed record ApiRewardReport(IReadOnlyList<ApiRewardReportItem> Items, int Total, int Page, int PageSize);

    private sealed record ApiLeaderboardEntry(
        Guid PlayerId, string? DisplayName, int Rank, int Points,
        int CorrectAnswers, int? CurrentLevel, string Status, int SecuredPoints);
    private sealed record ApiLeaderboard(Guid GameId, IReadOnlyList<ApiLeaderboardEntry> Players);

    private sealed record ApiOperationalMetrics(
        int TotalGames,
        IReadOnlyDictionary<string,int> ByStatus,
        int UniquePlayers,
        int ActivePlayers,
        IReadOnlyDictionary<string,int> DistributionByTenant,
        int TotalQuestions,
        IReadOnlyDictionary<string,int> ByCategory,
        IReadOnlyDictionary<int,int> ByLevel,
        int TotalCategories,
        int CategoriesInUse,
        IReadOnlyDictionary<string,int> QuestionsPerCategory);
    private sealed record ApiPerformanceMetrics(
        int TotalAnswers,
        int CorrectAnswers,
        int IncorrectAnswers,
        double AccuracyRate,
        int TotalPoints,
        double AverageScore,
        IReadOnlyDictionary<string,int> Distribution,
        IReadOnlyDictionary<string,int> ByTransactionType,
        int TotalWithdrawals,
        IReadOnlyDictionary<string,int> ByPolicy,
        double Rate);
    private sealed record ApiRewardsMetrics(
        int TotalRewards,
        IReadOnlyDictionary<string,int> ByType,
        IReadOnlyDictionary<string,int> ByStatus,
        int TotalRedemptions,
        IReadOnlyDictionary<string,int> RedemptionByStatus,
        IReadOnlyDictionary<string,int> RedemptionByType,
        int TotalCost,
        int TotalConsolations,
        int TotalCostConsolation,
        IReadOnlyDictionary<string,int> ByEligibility);
    private sealed record ApiReportSnapshot(
        ApiOperationalMetrics Operational,
        ApiPerformanceMetrics Performance,
        ApiRewardsMetrics Rewards,
        int TotalCount,
        DateTimeOffset CalculatedAt);
    private sealed record ApiReportSnapshotResponse(
        ApiOperationalMetrics Operational,
        ApiPerformanceMetrics Performance,
        ApiRewardsMetrics Rewards,
        int TotalCount,
        DateTimeOffset CalculatedAt);

    public async Task<ReportResult> GetGameReportAsync(Guid gameId, CancellationToken ct = default)
    {
        var report = await http.GetFromJsonAsync<ApiGameReport>($"{prefix}/reports/games/{gameId}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var rows = report.Players.Select(p => new object?[]
        {
            p.DisplayName ?? p.PlayerId.ToString(), p.Status,
            report.Rounds.Count(r => true),
            report.Winner is not null && report.Winner.PlayerId == p.PlayerId ? "Winner" : "-"
        }).ToList();
        return new ReportResult(
            $"Game report — {report.Name}",
            new DateRange(report.Start, report.End),
            ["Player", "Status", "Rounds", "Outcome"],
            rows);
    }

    public async Task<ReportResult> GetCategoryReportAsync(Guid categoryId, CancellationToken ct = default)
    {
        var report = await http.GetFromJsonAsync<ApiCategoryReport>($"{prefix}/reports/categories/{categoryId}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return new ReportResult(
            $"Category report — {report.CategoryName}",
            Period: null,
            ["Questions", "Games", "Players", "Average score", "Average accuracy"],
            [new object?[] { report.Questions, report.Games, report.Players, report.AverageScore, report.AverageAccuracy }]);
    }

    public async Task<ReportResult> GetQuestionReportAsync(Guid questionId, CancellationToken ct = default)
    {
        var report = await http.GetFromJsonAsync<ApiQuestionReport>($"{prefix}/reports/questions/{questionId}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return new ReportResult(
            $"Question report — {report.CategoryName}",
            Period: null,
            ["Difficulty", "Times presented", "Correct", "Incorrect", "Accuracy", "Avg. response time (s)"],
            [new object?[]
            {
                report.Difficulty, report.TimesPresented, report.CorrectAnswers,
                report.IncorrectAnswers, report.Accuracy, report.AverageResponseTime
            }]);
    }

    public async Task<ReportResult> GetPlayerReportAsync(Guid playerId, CancellationToken ct = default)
    {
        var report = await http.GetFromJsonAsync<ApiPlayerReport>($"{prefix}/reports/players/{playerId}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return new ReportResult(
            "Player report",
            Period: null,
            ["Games played", "Won", "Lost", "Withdrawn", "Questions answered", "Correct", "Accuracy", "Points earned", "Points redeemed"],
            [new object?[]
            {
                report.GamesPlayed, report.GamesWon, report.GamesLost, report.GamesWithdrawn,
                report.QuestionsAnswered, report.CorrectAnswers, report.Accuracy,
                report.PointsEarned, report.PointsRedeemed
            }]);
    }

    public async Task<ReportResult> GetRewardsReportAsync(DateRange? period, CancellationToken ct = default)
    {
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["from"] = period?.From?.ToString("O"),
            ["to"] = period?.To?.ToString("O")
        });
        var report = await http.GetFromJsonAsync<ApiRewardReport>($"{prefix}/reports/rewards{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return new ReportResult(
            "Rewards report",
            period,
            ["Reward", "Available stock", "Redemptions", "Points consumed", "Pending", "Delivered"],
            report.Items.Select(i => new object?[]
            {
                i.RewardName, i.AvailableStock, i.Redemptions, i.PointsConsumed, i.Pending, i.Delivered
            }).ToList());
    }

    public async Task<ReportResult> GetLeaderboardReportAsync(DateRange? period, CancellationToken ct = default)
    {
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["from"] = period?.From?.ToString("O"),
            ["to"] = period?.To?.ToString("O")
        });
        var report = await http.GetFromJsonAsync<ApiLeaderboard>($"{prefix}/reports/leaderboard{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return new ReportResult(
            "Leaderboard report",
            period,
            ["Rank", "Player", "Points", "Correct answers", "Secured points", "Status"],
            report.Players.OrderBy(p => p.Rank).Select(p => new object?[]
            {
                p.Rank, p.DisplayName ?? p.PlayerId.ToString(), p.Points,
                p.CorrectAnswers, p.SecuredPoints, p.Status
            }).ToList());
    }

    // 025 Admin Reporting — 12 métricas + 6 filtros
    public async Task<ReportsModels.ReportSnapshot> GetOperationalAsync(ReportsModels.ReportFilter filter, CancellationToken ct = default)
    {
        var validation = filter.Validate();
        if (validation.Count > 0) throw new ApiErrorException(new ApiErrorView("InvalidFilter", "Invalid filter", null, validation));
        var query = BuildReportQuery(filter);
        var response = await http.GetFromJsonAsync<ApiReportSnapshotResponse>($"{prefix}/reports/operational{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return MapSnapshot(filter, response);
    }

    public async Task<ReportsModels.ReportSnapshot> GetPerformanceAsync(ReportsModels.ReportFilter filter, CancellationToken ct = default)
    {
        var validation = filter.Validate();
        if (validation.Count > 0) throw new ApiErrorException(new ApiErrorView("InvalidFilter", "Invalid filter", null, validation));
        var query = BuildReportQuery(filter);
        var response = await http.GetFromJsonAsync<ApiReportSnapshotResponse>($"{prefix}/reports/performance{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return MapSnapshot(filter, response);
    }

    public async Task<ReportsModels.ReportSnapshot> GetRewardsAsync(ReportsModels.ReportFilter filter, CancellationToken ct = default)
    {
        var validation = filter.Validate();
        if (validation.Count > 0) throw new ApiErrorException(new ApiErrorView("InvalidFilter", "Invalid filter", null, validation));
        var query = BuildReportQuery(filter);
        var response = await http.GetFromJsonAsync<ApiReportSnapshotResponse>($"{prefix}/reports/rewards{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return MapSnapshot(filter, response);
    }

    public async Task<ReportsModels.ReportSnapshot> GetFullAsync(ReportsModels.ReportFilter filter, CancellationToken ct = default)
    {
        var validation = filter.Validate();
        if (validation.Count > 0) throw new ApiErrorException(new ApiErrorView("InvalidFilter", "Invalid filter", null, validation));
        var query = BuildReportQuery(filter);
        var response = await http.GetFromJsonAsync<ApiReportSnapshotResponse>($"{prefix}/reports/full{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return MapSnapshot(filter, response);
    }

    public async Task<PagedResult<ReportsModels.GameMetric>> GetOperationalRowsAsync(ReportsModels.ReportFilter filter, CancellationToken ct = default)
    {
        var validation = filter.Validate();
        if (validation.Count > 0) throw new ApiErrorException(new ApiErrorView("InvalidFilter", "Invalid filter", null, validation));
        var query = BuildReportQuery(filter);
        var response = await http.GetFromJsonAsync<ApiReportSnapshotResponse>($"{prefix}/reports/operational{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var snapshot = MapSnapshot(filter, response);
        var items = new List<ReportsModels.GameMetric> { snapshot.Operational.Games };
        return new PagedResult<ReportsModels.GameMetric>(items, snapshot.TotalCount, filter.Page, filter.PageSize);
    }

    private static string BuildReportQuery(ReportsModels.ReportFilter filter) => QueryString.Build(new Dictionary<string, string?>
    {
        ["from"] = filter.From?.ToString("O"),
        ["to"] = filter.To?.ToString("O"),
        ["categoryId"] = filter.CategoryId?.ToString(),
        ["categoryName"] = filter.CategoryName,
        ["gameId"] = filter.GameId?.ToString(),
        ["gameName"] = filter.GameName,
        ["playerId"] = filter.PlayerId?.ToString(),
        ["playerSearch"] = filter.PlayerSearch,
        ["level"] = filter.Level?.ToString(),
        ["result"] = filter.Result,
        ["page"] = filter.Page.ToString(),
        ["pageSize"] = filter.PageSize.ToString()
    });

    private static ReportsModels.ReportSnapshot MapSnapshot(ReportsModels.ReportFilter filter, ApiReportSnapshotResponse r)
    {
        var operational = new ReportsModels.OperationalMetrics(
            new ReportsModels.GameMetric(r.Operational.TotalGames, r.Operational.ByStatus),
            new ReportsModels.PlayerMetric(r.Operational.UniquePlayers, r.Operational.ActivePlayers, r.Operational.DistributionByTenant),
            new ReportsModels.QuestionMetric(r.Operational.TotalQuestions, r.Operational.ByCategory, r.Operational.ByLevel),
            new ReportsModels.CategoryMetric(r.Operational.TotalCategories, r.Operational.CategoriesInUse, r.Operational.QuestionsPerCategory));
        var performance = new ReportsModels.PerformanceMetrics(
            new ReportsModels.AnswerMetric(r.Performance.TotalAnswers, r.Performance.CorrectAnswers, r.Performance.IncorrectAnswers, r.Performance.AccuracyRate),
            new ReportsModels.ScoreMetric(r.Performance.TotalPoints, r.Performance.AverageScore, r.Performance.Distribution, r.Performance.ByTransactionType),
            new ReportsModels.WithdrawalMetric(r.Performance.TotalWithdrawals, r.Performance.ByPolicy, r.Performance.Rate));
        var rewards = new ReportsModels.RewardsMetrics(
            new ReportsModels.RewardMetric(r.Rewards.TotalRewards, r.Rewards.ByType, r.Rewards.ByStatus),
            new ReportsModels.RedemptionMetric(r.Rewards.TotalRedemptions, r.Rewards.RedemptionByStatus, r.Rewards.RedemptionByType, r.Rewards.TotalCost),
            new ReportsModels.ConsolationMetric(r.Rewards.TotalConsolations, r.Rewards.TotalCostConsolation, r.Rewards.ByEligibility));
        return new ReportsModels.ReportSnapshot(filter, operational, performance, rewards, r.TotalCount, r.CalculatedAt);
    }
}
