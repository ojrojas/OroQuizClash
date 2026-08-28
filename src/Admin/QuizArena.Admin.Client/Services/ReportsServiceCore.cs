using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;

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
}
