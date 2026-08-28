using QuizArena.Admin.Client.Models;
using ReportsModels = QuizArena.Admin.Client.Models.Reports;

namespace QuizArena.Admin.Client.Services;

public interface IReportsService
{
    Task<ReportResult> GetGameReportAsync(Guid gameId, CancellationToken ct = default);
    Task<ReportResult> GetCategoryReportAsync(Guid categoryId, CancellationToken ct = default);
    Task<ReportResult> GetQuestionReportAsync(Guid questionId, CancellationToken ct = default);
    Task<ReportResult> GetPlayerReportAsync(Guid playerId, CancellationToken ct = default);
    Task<ReportResult> GetRewardsReportAsync(DateRange? period, CancellationToken ct = default);
    Task<ReportResult> GetLeaderboardReportAsync(DateRange? period, CancellationToken ct = default);

    // 025 Admin Reporting — 12 métricas + 6 filtros
    Task<ReportsModels.ReportSnapshot> GetOperationalAsync(ReportsModels.ReportFilter filter, CancellationToken ct = default);
    Task<ReportsModels.ReportSnapshot> GetPerformanceAsync(ReportsModels.ReportFilter filter, CancellationToken ct = default);
    Task<ReportsModels.ReportSnapshot> GetRewardsAsync(ReportsModels.ReportFilter filter, CancellationToken ct = default);
    Task<ReportsModels.ReportSnapshot> GetFullAsync(ReportsModels.ReportFilter filter, CancellationToken ct = default);
    Task<PagedResult<ReportsModels.GameMetric>> GetOperationalRowsAsync(ReportsModels.ReportFilter filter, CancellationToken ct = default);
}
