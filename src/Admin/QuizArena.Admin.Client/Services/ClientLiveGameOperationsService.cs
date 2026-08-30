using System.Net.Http.Json;
using QuizArena.Admin.Client.Models.LiveGame;

namespace QuizArena.Admin.Client.Services;

public sealed class ClientLiveGameOperationsService(HttpClient http) : ILiveGameOperationsService
{
    public async Task<LiveGameView> PauseAsync(Guid gameId, string rowVersion, string idempotencyKey, CancellationToken ct = default)
        => await SendAsync(gameId, "pause", rowVersion, idempotencyKey, null, ct);

    public async Task<LiveGameView> ResumeAsync(Guid gameId, string rowVersion, string idempotencyKey, CancellationToken ct = default)
        => await SendAsync(gameId, "resume", rowVersion, idempotencyKey, null, ct);

    public async Task<LiveGameView> CancelAsync(Guid gameId, string rowVersion, string idempotencyKey, string? reason, CancellationToken ct = default)
        => await SendAsync(gameId, "cancel", rowVersion, idempotencyKey, reason, ct);

    public async Task<LiveGameView> ForceFinishAsync(Guid gameId, string rowVersion, string idempotencyKey, CancellationToken ct = default)
        => await SendAsync(gameId, "force-finish", rowVersion, idempotencyKey, null, ct);

    public async Task<LiveGameView> StartRoundAsync(Guid gameId, string rowVersion, string idempotencyKey, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/bff/games/{gameId}/rounds/start");
        req.Headers.TryAddWithoutValidation("If-Match", $"W/\"{rowVersion}\"");
        req.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);
        var response = await http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
        // Reload the live view after the operation
        var liveReq = new HttpRequestMessage(HttpMethod.Get, $"/bff/games/{gameId}/live");
        var liveResp = await http.SendAsync(liveReq, ct);
        return await liveResp.ReadAsAsync<LiveGameView>(ct);
    }

    public async Task<LiveGameView> CompleteRoundAsync(Guid gameId, Guid roundId, string rowVersion, string idempotencyKey, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/bff/games/{gameId}/rounds/{roundId}/complete")
        {
            Content = JsonContent.Create(new { rowVersion, idempotencyKey })
        };
        req.Headers.TryAddWithoutValidation("If-Match", $"W/\"{rowVersion}\"");
        req.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);
        var response = await http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
        // Reload the live view after the operation
        var liveReq = new HttpRequestMessage(HttpMethod.Get, $"/bff/games/{gameId}/live");
        var liveResp = await http.SendAsync(liveReq, ct);
        return await liveResp.ReadAsAsync<LiveGameView>(ct);
    }

    private async Task<LiveGameView> SendAsync(Guid gameId, string action, string rowVersion, string idempotencyKey, string? reason, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/bff/games/{gameId}/{action}")
        {
            Content = JsonContent.Create(new { rowVersion, idempotencyKey, reason })
        };
        req.Headers.TryAddWithoutValidation("If-Match", $"W/\"{rowVersion}\"");
        req.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);
        var response = await http.SendAsync(req, ct);
        var result = await response.ReadAsAsync<LiveGameView>(ct);
        return result;
    }
}
