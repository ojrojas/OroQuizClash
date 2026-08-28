using QuizArena.Admin.Client.Models.LiveGame;

namespace QuizArena.Admin.Client.Services;

public interface ILiveGameOperationsService
{
    Task<LiveGameView> PauseAsync(Guid gameId, string rowVersion, string idempotencyKey, CancellationToken ct = default);
    Task<LiveGameView> ResumeAsync(Guid gameId, string rowVersion, string idempotencyKey, CancellationToken ct = default);
    Task<LiveGameView> CancelAsync(Guid gameId, string rowVersion, string idempotencyKey, string? reason, CancellationToken ct = default);
    Task<LiveGameView> ForceFinishAsync(Guid gameId, string rowVersion, string idempotencyKey, CancellationToken ct = default);
}
