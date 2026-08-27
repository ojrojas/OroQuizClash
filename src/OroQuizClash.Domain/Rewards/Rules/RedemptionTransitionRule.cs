using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Rewards.Rules;

public sealed class RedemptionTransitionRule(RedemptionStatus current, RedemptionStatus target) : IBusinessRule
{
    public bool IsBroken()
    {
        if (current.IsTerminal)
            return true;

        if (current == RedemptionStatus.Requested &&
            (target == RedemptionStatus.Approved ||
             target == RedemptionStatus.Rejected ||
             target == RedemptionStatus.Cancelled))
            return false;

        if (current == RedemptionStatus.Approved &&
            (target == RedemptionStatus.Delivered ||
             target == RedemptionStatus.Cancelled))
            return false;

        return true;
    }

    public string Message => $"Cannot transition redemption from {current.Name} to {target.Name}.";
}
