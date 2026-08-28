using QuizArena.Admin.Client.Models;
using RewardsModels = QuizArena.Admin.Client.Models.Rewards;

namespace QuizArena.Admin.Client.Services;

public interface IRewardsService
{
    Task<PagedResult<Models.RewardSummary>> GetRewardsAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<PagedResult<RewardsModels.RewardSummary>> GetRewardsV2Async(RewardsModels.RewardFilter filter, CancellationToken ct = default);
    Task<RewardsModels.RewardDetail> GetRewardAsync(Guid id, CancellationToken ct = default);
    Task<Models.RewardSummary> CreateRewardAsync(Models.RewardForm form, CancellationToken ct = default);
    Task<RewardsModels.RewardDetail> CreateRewardAsync(RewardsModels.RewardForm form, CancellationToken ct = default);
    Task<Models.RewardSummary> UpdateRewardAsync(Guid rewardId, Models.RewardForm form, CancellationToken ct = default);
    Task<RewardsModels.RewardDetail> UpdateRewardAsync(Guid id, RewardsModels.UpdateRewardRequest request, CancellationToken ct = default);
    Task ActivateRewardAsync(Guid rewardId, CancellationToken ct = default);
    Task DeactivateRewardAsync(Guid rewardId, CancellationToken ct = default);
    Task<RewardsModels.RewardDetail> ActivateAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default);
    Task<RewardsModels.RewardDetail> DeactivateAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default);
    Task<RewardsModels.RewardDetail> ArchiveAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default);
}
