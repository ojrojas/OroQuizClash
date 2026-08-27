using BuildingBlocks.Kernel.Domain.Rules;

using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class AnswerImmutabilityRule(AnswerStatus status) : IBusinessRule
{
    public bool IsBroken() => status == AnswerStatus.Evaluated || status == AnswerStatus.Expired;
    public string Message => "Answer cannot be modified after evaluation.";
}
