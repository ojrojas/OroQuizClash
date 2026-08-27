using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Rewards;

public sealed class RewardStatus(int id, string name) : Enumeration<RewardStatus>(id, name)
{
    public static readonly RewardStatus Active = new(1, "ACTIVE");
    public static readonly RewardStatus Inactive = new(2, "INACTIVE");

    public bool IsActive => this == Active;
    public bool IsInactive => this == Inactive;
}
