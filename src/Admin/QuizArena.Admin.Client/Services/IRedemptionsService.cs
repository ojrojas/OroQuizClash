using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

public interface IRedemptionsService
{
    Task<PagedResult<RedemptionSummary>> GetRedemptionsAsync(RedemptionFilter filter, CancellationToken ct = default);
    Task ApproveAsync(Guid redemptionId, CancellationToken ct = default);
    Task RejectAsync(Guid redemptionId, CancellationToken ct = default);
    Task CancelAsync(Guid redemptionId, CancellationToken ct = default);
    Task DeliverAsync(Guid redemptionId, CancellationToken ct = default);
}
