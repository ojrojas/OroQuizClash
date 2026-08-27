using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Rewards.Rules;

public sealed class PointsRequiredPositiveRule(int pointsRequired) : IBusinessRule
{
    public bool IsBroken() => pointsRequired <= 0;
    public string Message => "Points required must be greater than zero.";
}
