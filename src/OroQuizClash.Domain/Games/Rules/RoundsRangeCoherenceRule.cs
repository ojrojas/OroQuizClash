using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class RoundsRangeCoherenceRule(int minRounds, int maxRounds) : IBusinessRule
{
    public bool IsBroken() => minRounds > maxRounds;
    public string Message => "MinRounds must be <= MaxRounds.";
}