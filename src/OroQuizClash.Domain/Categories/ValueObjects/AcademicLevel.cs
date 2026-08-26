using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Categories.ValueObjects;

public sealed class AcademicLevel : ValueObject
{
    public string Value { get; }

    public AcademicLevel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("AcademicLevel cannot be empty.", nameof(value));
        }

        var trimmed = value.Trim();

        if (trimmed.Length < 2 || trimmed.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), trimmed.Length, "AcademicLevel must be 2-100 characters.");
        }

        Value = trimmed;
    }

    private AcademicLevel()
    {
        Value = string.Empty;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}