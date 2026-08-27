using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Rewards.Rules;

public sealed class RewardNameValidRule(string name) : IBusinessRule
{
    public bool IsBroken() => string.IsNullOrWhiteSpace(name) || name.Trim().Length is < 3 or > 100;
    public string Message => "Reward name must be 3–100 characters and not whitespace.";
}
