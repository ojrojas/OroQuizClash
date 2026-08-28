using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

public interface IRewardsService
{
    Task<PagedResult<RewardSummary>> GetRewardsAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<RewardSummary> CreateRewardAsync(RewardForm form, CancellationToken ct = default);
    Task<RewardSummary> UpdateRewardAsync(Guid rewardId, RewardForm form, CancellationToken ct = default);
    Task ActivateRewardAsync(Guid rewardId, CancellationToken ct = default);
    Task DeactivateRewardAsync(Guid rewardId, CancellationToken ct = default);
}
