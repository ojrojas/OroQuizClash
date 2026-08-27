using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Rewards.Rules;

public sealed class StockNotNegativeRule(int stock) : IBusinessRule
{
    public bool IsBroken() => stock < 0;
    public string Message => "Stock must not be negative.";
}
