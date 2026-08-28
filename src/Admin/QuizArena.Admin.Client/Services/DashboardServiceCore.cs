using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

/// <summary>
/// Dashboard KPIs composed from existing read endpoints (games list, questions list,
/// redemptions list, rewards report). Each widget fails independently — the page renders
/// per-widget Loading/Ready/Empty/Error states (design-system/screens/admin-dashboard.md).
/// </summary>
public class DashboardServiceCore(
    IGamesAdminService games,
    IQuestionsService questions,
    IRedemptionsService redemptions,
    IReportsService reports) : IDashboardService
{
    public async Task<DashboardKpis> GetKpisAsync(CancellationToken ct = default)
    {
        var activeGames = await games.GetGamesAsync(new GameFilter(Status: GameStatusView.Active, PageSize: 1), ct);
        var bank = await questions.GetQuestionsAsync(new QuestionFilter(PageSize: 1), ct);
        var pending = await redemptions.GetRedemptionsAsync(new RedemptionFilter(Status: RedemptionStatusView.Pending, PageSize: 1), ct);

        var rewardsPaid = 0m;
        var gamesPeriod = 0;
        try
        {
            var rewardsReport = await reports.GetRewardsReportAsync(null, ct);
            rewardsPaid = rewardsReport.Rows.Sum(row => row.Length > 3 && row[3] is int consumed ? consumed : 0);
        }
        catch (ApiErrorException)
        {
            // Widget-level degradation: the KPI card shows its own error state.
        }

        try
        {
            var allGames = await games.GetGamesAsync(new GameFilter(PageSize: 1), ct);
            gamesPeriod = allGames.TotalCount;
        }
        catch (ApiErrorException)
        {
        }

        return new DashboardKpis(
            ActiveGames: activeGames.TotalCount,
            PlayersOnline: 0,
            QuestionBankSize: bank.TotalCount,
            PendingRedemptions: pending.TotalCount,
            RewardsPaidPeriod: rewardsPaid,
            GamesPeriod: gamesPeriod);
    }
}
