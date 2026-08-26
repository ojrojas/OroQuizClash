using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class MinRoundsAtLeastFiveRule(int minRounds) : IBusinessRule
{
    public bool IsBroken() => minRounds < 5;
    public string Message => "MinRounds must be >= 5.";
}