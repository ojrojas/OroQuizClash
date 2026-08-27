using BuildingBlocks.Kernel.Domain.Results;

namespace OroQuizClash.Domain.Rewards;

public static class RewardErrors
{
    public static Error RewardNotFound => Error.NotFound("RewardNotFound", "Reward not found.");
    public static Error InvalidRewardName => Error.Validation("Reward.InvalidName", "Reward name must be 3–100 characters and not whitespace.");
    public static Error InvalidRewardDescription => Error.Validation("Reward.InvalidDescription", "Reward description must be 3–500 characters.");
    public static Error InvalidPointsRequired => Error.Validation("Reward.InvalidPointsRequired", "Points required must be greater than zero.");
    public static Error InvalidStock => Error.Validation("Reward.InvalidStock", "Stock must not be negative.");
    public static Error RewardUnavailable => Error.Conflict("RewardUnavailable", "Reward is inactive, out of stock, or expired.");
    public static Error RewardAlreadyActive => Error.Conflict("Reward.InvalidStatusTransition", "Reward is already active.");
    public static Error RewardAlreadyInactive => Error.Conflict("Reward.InvalidStatusTransition", "Reward is already inactive.");

    public static Error RedemptionNotFound => Error.NotFound("RedemptionNotFound", "Redemption not found.");
    public static Error InvalidRedemptionTransition => Error.Conflict("Redemption.InvalidTransition", "Invalid redemption state transition.");
    public static Error NotRedemptionOwner => Error.Forbidden("Redemption.NotOwner", "Only the owning player can cancel this redemption.");
}
