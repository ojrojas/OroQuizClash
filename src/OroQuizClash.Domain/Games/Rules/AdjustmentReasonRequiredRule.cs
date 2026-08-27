using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class AdjustmentReasonRequiredRule(string? reason) : IBusinessRule
{
    public bool IsBroken() =>
        string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3 || reason.Trim().Length > 500;

    public string Message => "Adjustment reason must be 3-500 characters.";
}
