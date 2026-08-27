using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Questions.Rules;

public sealed class QuestionMustHaveDifficultyRule(int? difficultyId) : IBusinessRule
{
    public bool IsBroken() => difficultyId is null || difficultyId < 1 || difficultyId > 5;
    public string Message => "Question must have a difficulty between 1 and 5 (QST-004).";
}
