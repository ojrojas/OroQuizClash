using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

public interface IReportsService
{
    Task<ReportResult> GetGameReportAsync(Guid gameId, CancellationToken ct = default);
    Task<ReportResult> GetCategoryReportAsync(Guid categoryId, CancellationToken ct = default);
    Task<ReportResult> GetQuestionReportAsync(Guid questionId, CancellationToken ct = default);
    Task<ReportResult> GetPlayerReportAsync(Guid playerId, CancellationToken ct = default);
    Task<ReportResult> GetRewardsReportAsync(DateRange? period, CancellationToken ct = default);
    Task<ReportResult> GetLeaderboardReportAsync(DateRange? period, CancellationToken ct = default);
}
