using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;
using QuizArena.Admin.Client.Models.LiveGame;

namespace QuizArena.Admin.Client.Services;

public sealed class ClientLiveGameService(HttpClient http) : ILiveGameService
{
    public async Task<PagedResult<LiveGameView>> GetLiveGamesAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await http.GetFromJsonAsync<PagedResult<LiveGameView>>($"/bff/games?status=Running&page={page}&pageSize={pageSize}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return result;
    }

    public async Task<LiveGameView> GetLiveGameAsync(Guid gameId, CancellationToken ct = default)
    {
        // Try dedicated live endpoint first, fallback to composing from available calls
        try
        {
            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            opts.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            var live = await http.GetFromJsonAsync<LiveGameView>($"/bff/games/{gameId}/live", opts, ct);
            if (live is not null) return live;
        }
        catch (Exception) { }

        // Fallback: compose from game + leaderboard endpoints
        try
        {
            var gameOpts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var game = await http.GetFromJsonAsync<GameDetail>($"/bff/games/{gameId}", gameOpts, ct);
            IReadOnlyList<LiveScore> scores = [];
            try
            {
                var lb = await http.GetFromJsonAsync<IReadOnlyList<LiveScore>>($"/bff/games/{gameId}/leaderboard", ct);
                if (lb is not null) scores = lb;
            }
            catch { }

            if (game is not null)
            {
                return new LiveGameView(
                    gameId,
                    MapToGameState(game.Status),
                    game.RoundCount,
                    null,
                    null,
                    game.MaxRounds,
                    game.PlayerCount,
                    0, 0, 0,
                    scores,
                    0, 0,
                    game.RowVersion,
                    DateTimeOffset.UtcNow);
            }
        }
        catch { }

        throw new ApiErrorException(new ApiErrorView("LiveNotAvailable", "No se pudo cargar la vista en vivo."));
    }

    public async Task<IReadOnlyList<LiveScore>> GetLeaderboardAsync(Guid gameId, CancellationToken ct = default)
    {
        var result = await http.GetFromJsonAsync<IReadOnlyList<LiveScore>>($"/bff/games/{gameId}/leaderboard", ct);
        return result ?? [];
    }

    private static GameStateView MapToGameState(GameStatusView status) => status switch
    {
        GameStatusView.Configuring => GameStateView.Ready,
        GameStatusView.Lobby => GameStateView.Scheduled,
        GameStatusView.Active => GameStateView.Running,
        GameStatusView.Finished => GameStateView.Finished,
        GameStatusView.Cancelled => GameStateView.Cancelled,
        _ => GameStateView.Draft
    };
}
