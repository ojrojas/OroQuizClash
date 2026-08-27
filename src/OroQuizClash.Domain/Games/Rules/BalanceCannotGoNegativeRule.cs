using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class BalanceCannotGoNegativeRule(int currentPoints, int deduction) : IBusinessRule
{
    public bool IsBroken() => deduction > currentPoints;
    public string Message => "Deduction cannot exceed current balance; balance must not go negative.";
}
