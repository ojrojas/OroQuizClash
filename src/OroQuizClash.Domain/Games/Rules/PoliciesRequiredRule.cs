using BuildingBlocks.Kernel.Domain.Rules;

using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class PoliciesRequiredRule(LossPolicy? loss, WithdrawalPolicy? withdrawal) : IBusinessRule
{
    public bool IsBroken() => loss is null || withdrawal is null;
    public string Message => "Loss and withdrawal policies are required.";
}