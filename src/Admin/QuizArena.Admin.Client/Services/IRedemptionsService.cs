using QuizArena.Admin.Client.Models;
using RewardsModels = QuizArena.Admin.Client.Models.Rewards;

namespace QuizArena.Admin.Client.Services;

public interface IRedemptionsService
{
    Task<PagedResult<Models.RedemptionSummary>> GetRedemptionsAsync(Models.RedemptionFilter filter, CancellationToken ct = default);
    Task<PagedResult<RewardsModels.RewardRedemption>> GetRedemptionsV2Async(RewardsModels.RedemptionFilter filter, CancellationToken ct = default);
    Task<RewardsModels.RewardRedemption> GetRedemptionAsync(Guid id, CancellationToken ct = default);
    Task ApproveAsync(Guid redemptionId, CancellationToken ct = default);
    Task RejectAsync(Guid redemptionId, CancellationToken ct = default);
    Task CancelAsync(Guid redemptionId, CancellationToken ct = default);
    Task DeliverAsync(Guid redemptionId, CancellationToken ct = default);
    Task<RewardsModels.RewardRedemption> ApproveAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default);
    Task<RewardsModels.RewardRedemption> RejectAsync(Guid id, string rowVersion, string idempotencyKey, string reason, CancellationToken ct = default);
    Task<RewardsModels.RewardRedemption> DeliverAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default);
    Task<RewardsModels.RewardRedemption> CancelAsync(Guid id, string rowVersion, string idempotencyKey, string? reason, CancellationToken ct = default);
}
