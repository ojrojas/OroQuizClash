using QuizArena.Admin.Client.Models;
using QuizArena.Admin.Client.Models.Dashboard;

namespace QuizArena.Admin.Client.Services;

public interface IDashboardService
{
    Task<DashboardKpis> GetKpisAsync(CancellationToken ct = default);
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken ct = default);
    Task<MetricValue> GetMetricAsync(MetricId id, CancellationToken ct = default);
}
