using QuizArena.Admin.Client.Models;
using GC = QuizArena.Admin.Client.Models.GameConfiguration;

namespace QuizArena.Admin.Client.Services;

public interface IGameConfigurationService
{
    Task<GC.GameDetail> CreateAsync(GC.CreateGameRequest request, CancellationToken ct = default);
    Task<GC.GameDetail> UpdateAsync(Guid id, GC.UpdateGameRequest request, CancellationToken ct = default);
    Task<PagedResult<GC.GameSummary>> ListAsync(GC.GameFilter filter, CancellationToken ct = default);
    Task<GC.GameDetail> GetAsync(Guid id, CancellationToken ct = default);
    Task<GC.GameDetail> ScheduleAsync(Guid id, DateTimeOffset scheduledAt, string rowVersion, CancellationToken ct = default);
    Task<GC.GameDetail> ReadyAsync(Guid id, string rowVersion, CancellationToken ct = default);
    Task<GC.GameDetail> StartAsync(Guid id, string rowVersion, CancellationToken ct = default);
    Task<GC.GameDetail> PauseAsync(Guid id, string rowVersion, CancellationToken ct = default);
    Task<GC.GameDetail> ResumeAsync(Guid id, string rowVersion, CancellationToken ct = default);
    Task<GC.GameDetail> FinishAsync(Guid id, string rowVersion, CancellationToken ct = default);
    Task<GC.GameDetail> CancelAsync(Guid id, string rowVersion, CancellationToken ct = default);
}
