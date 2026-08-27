using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class PreviousQuestionNotRepeatedRule : IBusinessRule
{
    private readonly Guid _questionId;
    private readonly IReadOnlyList<Guid> _previousQuestionIds;

    public PreviousQuestionNotRepeatedRule(Guid questionId, IReadOnlyList<Guid> previousQuestionIds)
    {
        _questionId = questionId;
        _previousQuestionIds = previousQuestionIds;
    }

    public bool IsBroken() => _previousQuestionIds.Contains(_questionId);
    public string Message => "Question already used in this game.";
}
