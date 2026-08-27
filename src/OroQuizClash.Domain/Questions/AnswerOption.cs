using BuildingBlocks.Kernel.Domain.Entities;

namespace OroQuizClash.Domain.Questions;

public sealed class AnswerOption : Entity<AnswerOptionId>
{
    public QuestionId QuestionId { get; private set; } = null!;
    public string Text { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }
    public int DisplayOrder { get; private set; }

    private AnswerOption() { }

    internal AnswerOption(AnswerOptionId id, QuestionId questionId, string text, bool isCorrect, int displayOrder)
        : base(id)
    {
        QuestionId = questionId;
        Text = text;
        IsCorrect = isCorrect;
        DisplayOrder = displayOrder;
    }

    internal void UpdateText(string text) => Text = text;

    internal void SetCorrect(bool isCorrect) => IsCorrect = isCorrect;
}
