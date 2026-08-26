using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Categories.ValueObjects;

public sealed class KnowledgeArea : ValueObject
{
    public string Value { get; }

    public KnowledgeArea(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("KnowledgeArea cannot be empty.", nameof(value));
        }

        var trimmed = value.Trim();

        if (trimmed.Length < 2 || trimmed.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), trimmed.Length, "KnowledgeArea must be 2-100 characters.");
        }

        Value = trimmed;
    }

    private KnowledgeArea()
    {
        Value = string.Empty;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}