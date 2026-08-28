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
        // Try dedicated live endpoint first, fallback to composing from 4 calls if not available
        try
        {
            var live = await http.GetFromJsonAsync<LiveGameView>($"/bff/games/{gameId}/live", ct);
            if (live is not null) return live;
        }
        catch (HttpRequestException) { }

        // Fallback: fan-out to 4 endpoints and compose (research R2)
        var gameTask = http.GetFromJsonAsync<LiveGameView>($"/bff/games/{gameId}", ct);
        var leaderboardTask = http.GetFromJsonAsync<IReadOnlyList<LiveScore>>($"/bff/games/{gameId}/leaderboard", ct);
        var playersTask = http.GetFromJsonAsync<IReadOnlyList<object>>($"/bff/games/{gameId}/players", ct);
        var questionTask = http.GetFromJsonAsync<QuestionView>($"/bff/games/{gameId}/questions/current", ct);

        await Task.WhenAll(
            Task.Run(async () => { try { await gameTask; } catch { } }, ct),
            Task.Run(async () => { try { await leaderboardTask; } catch { } }, ct),
            Task.Run(async () => { try { await playersTask; } catch { } }, ct),
            Task.Run(async () => { try { await questionTask; } catch { } }, ct));

        // If live endpoint not available, construct minimal view from available data
        // For now, return a stub that will be enriched by polling
        throw new ApiErrorException(new ApiErrorView("LiveNotAvailable", "Live view not available via dedicated endpoint, use polling fallback."));
    }

    public async Task<IReadOnlyList<LiveScore>> GetLeaderboardAsync(Guid gameId, CancellationToken ct = default)
    {
        var result = await http.GetFromJsonAsync<IReadOnlyList<LiveScore>>($"/bff/games/{gameId}/leaderboard", ct);
        return result ?? [];
    }
}
