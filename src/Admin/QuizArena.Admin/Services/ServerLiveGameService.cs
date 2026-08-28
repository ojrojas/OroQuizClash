using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;
using QuizArena.Admin.Client.Models.LiveGame;
using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Services;

public sealed class ServerLiveGameService(HttpClient http) : ILiveGameService
{
    public async Task<PagedResult<LiveGameView>> GetLiveGamesAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await http.GetFromJsonAsync<PagedResult<LiveGameView>>($"/api/games?status=Running&page={page}&pageSize={pageSize}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return result;
    }

    public async Task<LiveGameView> GetLiveGameAsync(Guid gameId, CancellationToken ct = default)
    {
        try
        {
            var live = await http.GetFromJsonAsync<LiveGameView>($"/api/games/{gameId}/live", ct);
            if (live is not null) return live;
        }
        catch (HttpRequestException) { }

        // Fallback fan-out
        throw new ApiErrorException(new ApiErrorView("LiveNotAvailable", "Live view not available"));
    }

    public async Task<IReadOnlyList<LiveScore>> GetLeaderboardAsync(Guid gameId, CancellationToken ct = default)
    {
        var result = await http.GetFromJsonAsync<IReadOnlyList<LiveScore>>($"/api/games/{gameId}/leaderboard", ct);
        return result ?? [];
    }
}
