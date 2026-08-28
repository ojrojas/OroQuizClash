using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;
using PlayersModels = QuizArena.Admin.Client.Models.Players;

namespace QuizArena.Admin.Client.Services;

public class PlayersServiceCore(HttpClient http, string prefix) : IPlayersService
{
    private sealed record ApiParticipationStatus(
        Guid GameId,
        Guid PlayerId,
        string ParticipationStatus,
        int CurrentPoints,
        int SecuredPoints,
        DateTimeOffset? ExitedAt);

    private sealed record ApiConsolationItem(
        Guid GameId,
        string GameName,
        string Policy,
        int? Points,
        string? RewardName,
        DateTimeOffset Timestamp);

    private sealed record ApiConsolationHistory(Guid PlayerId, IReadOnlyList<ApiConsolationItem> Consolations);

    private sealed record ApiLeaderboardEntry(
        Guid PlayerId,
        string? DisplayName,
        int Rank,
        int Points,
        string Status,
        int SecuredPoints);

    private sealed record ApiLeaderboard(Guid GameId, IReadOnlyList<ApiLeaderboardEntry> Players);

    private sealed record ApiPlayerSummary(
        Guid PlayerId,
        string DisplayName,
        string Email,
        string? TenantId,
        string? IdentificationType,
        string? IdentificationValue,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastActiveAt,
        string State);

    private sealed record ApiPlayersResponse(IReadOnlyList<ApiPlayerSummary> Items, int TotalCount, int Page, int PageSize);
    private sealed record ApiPlayerDetail(
        Guid PlayerId,
        string DisplayName,
        string Email,
        string? TenantId,
        string? IdentificationType,
        string? IdentificationValue,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastActiveAt,
        string State,
        ApiScoreSummary? ScoreSummary,
        int TotalParticipations,
        string RowVersion);
    private sealed record ApiScoreSummary(int TotalPoints, int SecuredPoints, int AvailablePoints);
    private sealed record ApiGameHistoryEntry(
        Guid GameId,
        string GameName,
        Guid CategoryId,
        string CategoryName,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? StartedAt,
        DateTimeOffset? FinishedAt,
        int RoundCount,
        int? PlayerScore,
        int? PlayerRank);
    private sealed record ApiGameHistoryResponse(IReadOnlyList<ApiGameHistoryEntry> Items, int TotalCount, int Page, int PageSize);
    private sealed record ApiParticipation(
        Guid ParticipationId,
        Guid GameId,
        string GameName,
        DateTimeOffset JoinedAt,
        string State,
        string GameStatus,
        string? Role);
    private sealed record ApiParticipationsResponse(IReadOnlyList<ApiParticipation> Items, int TotalCount, int Page, int PageSize);
    private sealed record ApiPlayerResult(
        Guid PlayerId,
        Guid GameId,
        int TotalScore,
        int SecuredScore,
        int Rank,
        int CorrectAnswers,
        int TotalAnswers,
        TimeSpan Duration,
        IReadOnlyList<ApiTransaction> Bonuses,
        IReadOnlyList<ApiTransaction> Penalties);
    private sealed record ApiTransaction(
        Guid TransactionId,
        Guid PlayerId,
        Guid GameId,
        string Type,
        int Points,
        DateTimeOffset Timestamp,
        Guid? ReferenceId);
    private sealed record ApiScoresResponse(IReadOnlyList<ApiTransaction> Items, int TotalCount, int Page, int PageSize);
    private sealed record ApiRedemption(
        Guid RedemptionId,
        Guid RewardId,
        string RewardName,
        string RewardType,
        int Cost,
        string Status,
        DateTimeOffset RequestedAt,
        DateTimeOffset? ApprovedAt,
        DateTimeOffset? DeliveredAt,
        string? Reason,
        bool IsConsolation,
        string RowVersion);
    private sealed record ApiRedemptionsResponse(IReadOnlyList<ApiRedemption> Items, int TotalCount, int Page, int PageSize);
    private sealed record ApiStatistics(
        Guid PlayerId,
        int TotalGames,
        int Wins,
        int Top3,
        double AverageScore,
        double AccuracyRate,
        int BestStreak,
        TimeSpan AverageTimePerQuestion,
        IReadOnlyDictionary<string,int> DistributionByDifficulty,
        IReadOnlyDictionary<string,int> DistributionByCategory,
        DateTimeOffset CalculatedAt);

    public async Task<PlayerStatusView> GetPlayerStatusAsync(Guid gameId, Guid playerId, CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<ApiParticipationStatus>(
            $"{prefix}/games/{gameId}/players/{playerId}/status", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return new PlayerStatusView(
            response.PlayerId, DisplayName: null, response.GameId, response.ParticipationStatus,
            response.CurrentPoints, response.SecuredPoints, response.ExitedAt);
    }

    public async Task<IReadOnlyList<ConsolationHistoryEntry>> GetConsolationHistoryAsync(Guid playerId, CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<ApiConsolationHistory>(
            $"{prefix}/players/{playerId}/consolation-history", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return response.Consolations
            .Select(c => new ConsolationHistoryEntry(
                c.GameId, c.GameName, c.Policy, c.Points, c.RewardName, c.Timestamp))
            .ToList();
    }

    public async Task<PagedResult<PlayerStatusView>> GetGamePlayersAsync(Guid gameId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        // The API exposes per-game players through the leaderboard aggregate.
        var leaderboard = await http.GetFromJsonAsync<ApiLeaderboard>($"{prefix}/games/{gameId}/leaderboard", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var players = leaderboard.Players
            .Select(p => new PlayerStatusView(
                p.PlayerId, p.DisplayName, gameId, p.Status, p.Points, p.SecuredPoints, ExitedAt: null))
            .ToList();
        var pageItems = players.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<PlayerStatusView>(pageItems, players.Count, page, pageSize);
    }

    // 024 Admin Players — solo lectura
    public async Task<PagedResult<PlayersModels.PlayerSummary>> GetPlayersAsync(PlayersModels.PlayerFilter filter, CancellationToken ct = default)
    {
        var validation = filter.Validate();
        if (validation.Count > 0) throw new ApiErrorException(new ApiErrorView("InvalidFilter", "Invalid filter", null, validation));
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["search"] = filter.Search,
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        });
        var response = await http.GetFromJsonAsync<ApiPlayersResponse>($"{prefix}/players{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var items = response.Items.Select(MapPlayer).ToList();
        return new PagedResult<PlayersModels.PlayerSummary>(items, response.TotalCount, response.Page, response.PageSize);
    }

    public async Task<PlayersModels.PlayerDetail> GetPlayerAsync(Guid playerId, CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<ApiPlayerDetail>($"{prefix}/players/{playerId}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return MapDetail(response);
    }

    public async Task<PagedResult<PlayersModels.GameHistoryEntry>> GetPlayerGamesAsync(Guid playerId, PlayersModels.GameHistoryFilter filter, CancellationToken ct = default)
    {
        var validation = filter.Validate();
        if (validation.Count > 0) throw new ApiErrorException(new ApiErrorView("InvalidFilter", "Invalid filter", null, validation));
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["search"] = filter.Search,
            ["status"] = filter.Status,
            ["from"] = filter.From?.ToString("O"),
            ["to"] = filter.To?.ToString("O"),
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        });
        var response = await http.GetFromJsonAsync<ApiGameHistoryResponse>($"{prefix}/players/{playerId}/games{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var items = response.Items.Select(MapHistory).ToList();
        return new PagedResult<PlayersModels.GameHistoryEntry>(items, response.TotalCount, response.Page, response.PageSize);
    }

    public async Task<PagedResult<PlayersModels.PlayerParticipation>> GetParticipationsAsync(Guid playerId, PlayersModels.ParticipationFilter filter, CancellationToken ct = default)
    {
        var validation = filter.Validate();
        if (validation.Count > 0) throw new ApiErrorException(new ApiErrorView("InvalidFilter", "Invalid filter", null, validation));
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["state"] = filter.State,
            ["from"] = filter.From?.ToString("O"),
            ["to"] = filter.To?.ToString("O"),
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        });
        var response = await http.GetFromJsonAsync<ApiParticipationsResponse>($"{prefix}/players/{playerId}/participations{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var items = response.Items.Select(MapParticipation).ToList();
        return new PagedResult<PlayersModels.PlayerParticipation>(items, response.TotalCount, response.Page, response.PageSize);
    }

    public async Task<PlayersModels.PlayerResult> GetResultAsync(Guid playerId, Guid gameId, CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<ApiPlayerResult>($"{prefix}/players/{playerId}/results/{gameId}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return MapResult(response);
    }

    public async Task<PagedResult<PlayersModels.PointTransactionView>> GetScoresAsync(Guid playerId, PlayersModels.ScoreFilter filter, CancellationToken ct = default)
    {
        var validation = filter.Validate();
        if (validation.Count > 0) throw new ApiErrorException(new ApiErrorView("InvalidFilter", "Invalid filter", null, validation));
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["type"] = filter.Type?.ToString(),
            ["from"] = filter.From?.ToString("O"),
            ["to"] = filter.To?.ToString("O"),
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        });
        var response = await http.GetFromJsonAsync<ApiScoresResponse>($"{prefix}/players/{playerId}/scores{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var items = response.Items.Select(MapTransaction).ToList();
        return new PagedResult<PlayersModels.PointTransactionView>(items, response.TotalCount, response.Page, response.PageSize);
    }

    public async Task<PagedResult<PlayersModels.PlayerRedemptionView>> GetRedemptionsAsync(Guid playerId, PlayersModels.RedemptionFilter filter, CancellationToken ct = default)
    {
        var validation = filter.Validate();
        if (validation.Count > 0) throw new ApiErrorException(new ApiErrorView("InvalidFilter", "Invalid filter", null, validation));
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["status"] = filter.Status,
            ["rewardType"] = filter.RewardType,
            ["from"] = filter.From?.ToString("O"),
            ["to"] = filter.To?.ToString("O"),
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        });
        var response = await http.GetFromJsonAsync<ApiRedemptionsResponse>($"{prefix}/players/{playerId}/redemptions{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var items = response.Items.Select(MapRedemption).ToList();
        return new PagedResult<PlayersModels.PlayerRedemptionView>(items, response.TotalCount, response.Page, response.PageSize);
    }

    public async Task<PlayersModels.PlayerStatistics> GetStatisticsAsync(Guid playerId, CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<ApiStatistics>($"{prefix}/players/{playerId}/statistics", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return new PlayersModels.PlayerStatistics(
            response.PlayerId, response.TotalGames, response.Wins, response.Top3,
            response.AverageScore, response.AccuracyRate, response.BestStreak,
            response.AverageTimePerQuestion, response.DistributionByDifficulty,
            response.DistributionByCategory, response.CalculatedAt);
    }

    private static PlayersModels.PlayerSummary MapPlayer(ApiPlayerSummary r) => new(
        r.PlayerId, r.DisplayName, r.Email, r.TenantId, r.IdentificationType, r.IdentificationValue,
        r.CreatedAt, r.LastActiveAt, PlayersModels.PlayerStateMap.FromApi(r.State));

    private static PlayersModels.PlayerDetail MapDetail(ApiPlayerDetail r) => new(
        r.PlayerId, r.DisplayName, r.Email, r.TenantId, r.IdentificationType, r.IdentificationValue,
        r.CreatedAt, r.LastActiveAt, PlayersModels.PlayerStateMap.FromApi(r.State),
        r.ScoreSummary is null ? new PlayersModels.PlayerScoreSummary(0,0,0) : new PlayersModels.PlayerScoreSummary(r.ScoreSummary.TotalPoints, r.ScoreSummary.SecuredPoints, r.ScoreSummary.AvailablePoints),
        r.TotalParticipations, r.RowVersion);

    private static PlayersModels.GameHistoryEntry MapHistory(ApiGameHistoryEntry r) => new(
        r.GameId, r.GameName, r.CategoryId, r.CategoryName, r.Status, r.CreatedAt, r.StartedAt, r.FinishedAt, r.RoundCount, r.PlayerScore, r.PlayerRank);

    private static PlayersModels.PlayerParticipation MapParticipation(ApiParticipation r) => new(
        r.ParticipationId, r.GameId, r.GameName, r.JoinedAt, r.State, r.GameStatus, r.Role);

    private static PlayersModels.PlayerResult MapResult(ApiPlayerResult r) => new(
        r.PlayerId, r.GameId, r.TotalScore, r.SecuredScore, r.Rank, r.CorrectAnswers, r.TotalAnswers, r.Duration,
        r.Bonuses.Select(MapTransaction).ToList(), r.Penalties.Select(MapTransaction).ToList());

    private static PlayersModels.PointTransactionView MapTransaction(ApiTransaction r) => new(
        r.TransactionId, r.PlayerId, r.GameId, PlayersModels.TransactionTypeMap.FromApi(r.Type), r.Points, r.Timestamp, r.ReferenceId);

    private static PlayersModels.PlayerRedemptionView MapRedemption(ApiRedemption r) => new(
        r.RedemptionId, r.RewardId, r.RewardName, r.RewardType, r.Cost, r.Status, r.RequestedAt, r.ApprovedAt, r.DeliveredAt, r.Reason, r.IsConsolation, r.RowVersion);
}
