using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

public interface IDashboardService
{
    Task<DashboardKpis> GetKpisAsync(CancellationToken ct = default);
}
