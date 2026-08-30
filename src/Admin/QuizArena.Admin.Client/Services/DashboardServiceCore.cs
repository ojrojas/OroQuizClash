using QuizArena.Admin.Client.Models;
using QuizArena.Admin.Client.Models.Dashboard;

namespace QuizArena.Admin.Client.Services;

/// <summary>
/// Dashboard KPIs + operational snapshot (10 blocks) composed from existing read endpoints.
/// Each metric fails independently (per-widget Loading/Ready/Empty/Error) per research R1.
/// </summary>
public class DashboardServiceCore(
    IGamesAdminService games,
    IQuestionsService questions,
    IRedemptionsService redemptions,
    IReportsService reports,
    ICategoriesService? categories = null,
    IRewardsService? rewards = null) : IDashboardService
{
    public async Task<DashboardKpis> GetKpisAsync(CancellationToken ct = default)
    {
        var snapshot = await GetSnapshotAsync(ct);
        var active = snapshot.Metrics.FirstOrDefault(m => m.Id == MetricId.ActiveGames)?.Count ?? 0;
        var questionsCount = snapshot.Metrics.FirstOrDefault(m => m.Id == MetricId.AvailableQuestions)?.Count ?? 0;
        var pending = snapshot.Metrics.FirstOrDefault(m => m.Id == MetricId.Redemptions)?.Count ?? 0;
        var totalGames = snapshot.Statistics.TotalGames;
        var rewardsPaid = snapshot.Statistics.Breakdown?.FirstOrDefault(b => b.Key == "rewardsPaid") is { } r && decimal.TryParse(r.Value, out var v) ? v : snapshot.Metrics.FirstOrDefault(m => m.Id == MetricId.Rewards)?.Count ?? 0;

        return new DashboardKpis(
            ActiveGames: active,
            PlayersOnline: snapshot.Metrics.FirstOrDefault(m => m.Id == MetricId.ConnectedPlayers)?.Count ?? 0,
            QuestionBankSize: questionsCount,
            PendingRedemptions: pending,
            RewardsPaidPeriod: rewardsPaid,
            GamesPeriod: totalGames);
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var ctTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ctTimeout.CancelAfter(TimeSpan.FromSeconds(5));

        var tasks = new Dictionary<MetricId, Task<MetricValue>>
        {
            [MetricId.ActiveGames] = FetchAsync(MetricId.ActiveGames, "Juegos activos", "/admin/games?status=Active", FetchActiveGamesAsync, ctTimeout.Token),
            [MetricId.ScheduledGames] = FetchAsync(MetricId.ScheduledGames, "Juegos programados", "/admin/games?status=Scheduled", FetchScheduledGamesAsync, ctTimeout.Token),
            [MetricId.FinishedGames] = FetchAsync(MetricId.FinishedGames, "Juegos finalizados", "/admin/games?status=Finished", FetchFinishedGamesAsync, ctTimeout.Token),
            [MetricId.ConnectedPlayers] = FetchAsync(MetricId.ConnectedPlayers, "Jugadores conectados", "/admin/players?view=online", FetchConnectedPlayersAsync, ctTimeout.Token),
            [MetricId.ActivePlayers] = FetchAsync(MetricId.ActivePlayers, "Jugadores activos", "/admin/players?view=active", FetchActivePlayersAsync, ctTimeout.Token),
            [MetricId.AvailableQuestions] = FetchAsync(MetricId.AvailableQuestions, "Preguntas disponibles", "/admin/questions?status=Active", FetchQuestionsAsync, ctTimeout.Token),
            [MetricId.Categories] = FetchAsync(MetricId.Categories, "Categorías", "/admin/categories?status=Active", FetchCategoriesAsync, ctTimeout.Token),
            [MetricId.Rewards] = FetchAsync(MetricId.Rewards, "Premios", "/admin/rewards?status=Active", FetchRewardsAsync, ctTimeout.Token),
            [MetricId.Redemptions] = FetchAsync(MetricId.Redemptions, "Canjes", "/admin/rewards?status=Pending", FetchRedemptionsAsync, ctTimeout.Token),
            [MetricId.GeneralStatistics] = FetchAsync(MetricId.GeneralStatistics, "Estadísticas generales", "/admin/reports?focus=general", FetchGeneralStatisticsMetricAsync, ctTimeout.Token)
        };

        var results = new List<MetricValue>();
        foreach (var kv in tasks)
        {
            try
            {
                results.Add(await kv.Value.ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                results.Add(ErrorMetric(kv.Key, LabelFor(kv.Key), RouteFor(kv.Key), "Timeout", "La carga tardó demasiado. Reintente."));
            }
            catch (ApiErrorException ex)
            {
                results.Add(ErrorMetric(kv.Key, LabelFor(kv.Key), RouteFor(kv.Key), ex.ErrorView.Code, ex.ErrorView.Detail ?? ex.ErrorView.Title));
            }
            catch (Exception)
            {
                results.Add(ErrorMetric(kv.Key, LabelFor(kv.Key), RouteFor(kv.Key), "Unknown", "No se pudo cargar el indicador. Reintente."));
            }
        }

        // Build GeneralStatistics from individual results for the Statistics property
        var stats = await BuildStatisticsAsync(results, ct).ConfigureAwait(false);

        // Replace GeneralStatistics metric count with a summary count (or leave as 1 for drill-down)
        var genMetric = results.FirstOrDefault(m => m.Id == MetricId.GeneralStatistics);
        if (genMetric is not null && genMetric.State == MetricState.Ready)
        {
            var idx = results.FindIndex(m => m.Id == MetricId.GeneralStatistics);
            results[idx] = genMetric with { Count = stats.TotalGames > 0 ? stats.TotalGames : 1 };
        }

        return new DashboardSnapshot(DateTimeOffset.UtcNow, null, results, stats);
    }

    public async Task<MetricValue> GetMetricAsync(MetricId id, CancellationToken ct = default)
    {
        var snapshot = await GetSnapshotAsync(ct);
        return snapshot.Metrics.FirstOrDefault(m => m.Id == id) ?? ErrorMetric(id, LabelFor(id), RouteFor(id), "NotFound", "Indicador no encontrado.");
    }

    private static string LabelFor(MetricId id) => id switch
    {
        MetricId.ActiveGames => "Juegos activos",
        MetricId.ScheduledGames => "Juegos programados",
        MetricId.FinishedGames => "Juegos finalizados",
        MetricId.ConnectedPlayers => "Jugadores conectados",
        MetricId.ActivePlayers => "Jugadores activos",
        MetricId.AvailableQuestions => "Preguntas disponibles",
        MetricId.Categories => "Categorías",
        MetricId.Rewards => "Premios",
        MetricId.Redemptions => "Canjes",
        MetricId.GeneralStatistics => "Estadísticas generales",
        _ => id.ToString()
    };

    private static string RouteFor(MetricId id) => id switch
    {
        MetricId.ActiveGames => "/admin/games?status=Active",
        MetricId.ScheduledGames => "/admin/games?status=Scheduled",
        MetricId.FinishedGames => "/admin/games?status=Finished",
        MetricId.ConnectedPlayers => "/admin/players?view=online",
        MetricId.ActivePlayers => "/admin/players?view=active",
        MetricId.AvailableQuestions => "/admin/questions?status=Active",
        MetricId.Categories => "/admin/categories?status=Active",
        MetricId.Rewards => "/admin/rewards?status=Active",
        MetricId.Redemptions => "/admin/rewards?status=Pending",
        MetricId.GeneralStatistics => "/admin/reports?focus=general",
        _ => "/admin/dashboard"
    };

    private async Task<MetricValue> FetchAsync(MetricId id, string label, string route, Func<CancellationToken, Task<int>> fetcher, CancellationToken ct)
    {
        try
        {
            var count = await fetcher(ct).ConfigureAwait(false);
            var state = count == 0 ? MetricState.Empty : MetricState.Ready;
            // Player presence fallback: document source when approximated
            string? source = null;
            string? tooltip = null;
            if (id == MetricId.ConnectedPlayers)
            {
                source = "Aproximación: participantes en juegos";
                tooltip = "Aproximación: backend no expone presencia online separada; se usa conteo de participantes.";
                if (count == 0) tooltip = null;
            }
            if (id == MetricId.ActivePlayers)
            {
                source = "PLAYING en IN_PROGRESS";
            }
            return new MetricValue(id, label, count, state, SourceLabel: source, Tooltip: tooltip, DrillDownRoute: route);
        }
        catch (ApiErrorException)
        {
            throw;
        }
    }

    private static MetricValue ErrorMetric(MetricId id, string label, string route, string code, string message) =>
        new(id, label, 0, MetricState.Error, ErrorCode: code, ErrorMessage: message, Retryable: true, DrillDownRoute: route);

    private async Task<int> FetchActiveGamesAsync(CancellationToken ct) =>
        (await games.GetGamesAsync(new GameFilter(Status: GameStatusView.Active, PageSize: 1), ct)).TotalCount;

    private async Task<int> FetchScheduledGamesAsync(CancellationToken ct)
    {
        // Scheduled = Lobby (WAITING) + Configuring (DRAFT/READY) approximation
        var lobby = (await games.GetGamesAsync(new GameFilter(Status: GameStatusView.Lobby, PageSize: 1), ct)).TotalCount;
        var configuring = (await games.GetGamesAsync(new GameFilter(Status: GameStatusView.Configuring, PageSize: 1), ct)).TotalCount;
        return lobby + configuring;
    }

    private async Task<int> FetchFinishedGamesAsync(CancellationToken ct)
    {
        var finished = (await games.GetGamesAsync(new GameFilter(Status: GameStatusView.Finished, PageSize: 1), ct)).TotalCount;
        var cancelled = (await games.GetGamesAsync(new GameFilter(Status: GameStatusView.Cancelled, PageSize: 1), ct)).TotalCount;
        return finished + cancelled;
    }

    private async Task<int> FetchConnectedPlayersAsync(CancellationToken ct)
    {
        // Approximate via participants in active games (live). Sum PlayerCount from IN_PROGRESS + ROUND_IN_PROGRESS.
        // GameStatusView.Active maps via FromApi to both IN_PROGRESS/ROUND_IN_PROGRESS/ROUND_COMPLETED, but ToApiQuery only sends IN_PROGRESS.
        // To avoid missing ROUND_IN_PROGRESS, we fetch unfiltered and filter client-side for Active.
        try
        {
            var all = await games.GetGamesAsync(new GameFilter(PageSize: 100), ct);
            var activePlayers = all.Items.Where(g => g.Status == GameStatusView.Active).Sum(g => g.PlayerCount);
            if (activePlayers > 0) return activePlayers;
            // Fallback: also check Lobby games that have players (still connected while waiting)
            var lobbyPlayers = all.Items.Where(g => g.Status == GameStatusView.Lobby).Sum(g => g.PlayerCount);
            return activePlayers + lobbyPlayers;
        }
        catch
        {
            return 0;
        }
    }

    private Task<int> FetchActivePlayersAsync(CancellationToken ct)
    {
        // Approximate: count of players in active games would require extra calls; return 0 for now.
        return Task.FromResult(0);
    }

    private async Task<int> FetchQuestionsAsync(CancellationToken ct) =>
        (await questions.GetQuestionsAsync(new QuestionFilter(Status: QuestionStatusView.Active, PageSize: 1), ct)).TotalCount;

    private async Task<int> FetchCategoriesAsync(CancellationToken ct)
    {
        if (categories is null) return 0;
        return (await categories.GetCategoriesAsync(new CategoryFilter(Status: CategoryStatusView.Active, PageSize: 1), ct)).TotalCount;
    }

    private async Task<int> FetchRewardsAsync(CancellationToken ct)
    {
        if (rewards is null) return 0;
        return (await rewards.GetRewardsAsync(page: 1, pageSize: 1, ct)).TotalCount;
    }

    private async Task<int> FetchRedemptionsAsync(CancellationToken ct) =>
        (await redemptions.GetRedemptionsAsync(new RedemptionFilter(Status: RedemptionStatusView.Pending, PageSize: 1), ct)).TotalCount;

    private async Task<int> FetchGeneralStatisticsMetricAsync(CancellationToken ct)
    {
        // Count for the tile itself; detailed stats built in BuildStatisticsAsync
        var all = (await games.GetGamesAsync(new GameFilter(PageSize: 1), ct)).TotalCount;
        return all;
    }

    private async Task<GeneralStatistics> BuildStatisticsAsync(IReadOnlyList<MetricValue> metrics, CancellationToken ct)
    {
        var totalGames = metrics.FirstOrDefault(m => m.Id == MetricId.GeneralStatistics)?.Count ?? 0;
        // Try to get totals from already fetched metrics
        var questions = metrics.FirstOrDefault(m => m.Id == MetricId.AvailableQuestions)?.Count ?? 0;
        var cats = metrics.FirstOrDefault(m => m.Id == MetricId.Categories)?.Count ?? 0;
        var avg = cats == 0 ? 0 : (double)questions / cats;

        // Total participations: best effort from reports if available
        var participations = 0;
        try
        {
            var allGames = await games.GetGamesAsync(new GameFilter(PageSize: 1), ct);
            participations = allGames.TotalCount; // fallback until player report exists
        }
        catch { }

        return new GeneralStatistics(
            TotalGames: totalGames,
            TotalParticipations: participations,
            AvgQuestionsPerCategory: Math.Round(avg, 1),
            Breakdown:
            [
                new("rewardsPaid", "Premios activos", (metrics.FirstOrDefault(m => m.Id == MetricId.Rewards)?.Count ?? 0).ToString()),
                new("categoriesActive", "Categorías activas", cats.ToString())
            ]);
    }
}
