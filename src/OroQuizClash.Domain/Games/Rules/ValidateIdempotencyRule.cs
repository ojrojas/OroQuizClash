using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class ValidateIdempotencyRule(bool answerExists) : IBusinessRule
{
    public bool IsBroken() => false;
    public string Message => "Answer already exists for this round.";

    public bool AnswerAlreadyExists => answerExists;
}
