using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Categories.Rules;

public sealed class AgeRangeCoherentRule(int min, int max) : IBusinessRule
{
    public bool IsBroken() => min < 0 || max < 0 || max > 120 || min > max;
    public string Message => "Age range invalid: min must be <= max and between 0 and 120.";
}