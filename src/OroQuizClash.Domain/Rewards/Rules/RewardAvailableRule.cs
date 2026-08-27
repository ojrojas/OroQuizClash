using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Rewards.Rules;

public sealed class RewardAvailableRule(RewardStatus status, int stock, DateTimeOffset? expirationDate, DateTimeOffset now) : IBusinessRule
{
    public bool IsBroken() =>
        !status.IsActive ||
        stock <= 0 ||
        (expirationDate.HasValue && expirationDate.Value <= now);

    public string Message => "Reward is inactive, out of stock, or expired.";
}
