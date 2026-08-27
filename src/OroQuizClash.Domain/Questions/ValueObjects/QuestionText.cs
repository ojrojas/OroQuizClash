using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Questions.ValueObjects;

public sealed class QuestionText : ValueObject
{
    public string Value { get; }

    private QuestionText(string value) => Value = value;

    public static QuestionText Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Question text must not be empty.", nameof(value));

        var trimmed = value.Trim();
        if (trimmed.Length < 3 || trimmed.Length > 500)
            throw new ArgumentException("Question text must be 3-500 characters.", nameof(value));

        return new QuestionText(trimmed);
    }

    public static bool TryCreate(string value, out QuestionText? result, out string? error)
    {
        result = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value)) { error = "Text must not be empty."; return false; }
        var trimmed = value.Trim();
        if (trimmed.Length < 3 || trimmed.Length > 500) { error = "Text must be 3-500 characters."; return false; }
        result = new QuestionText(trimmed);
        return true;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(QuestionText text) => text.Value;
}
