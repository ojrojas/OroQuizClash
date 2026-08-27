using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Questions.ValueObjects;

public sealed class AcademicLevel : ValueObject
{
    public string Value { get; }

    public AcademicLevel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("AcademicLevel must not be empty.", nameof(value));
        var trimmed = value.Trim();
        if (trimmed.Length < 2 || trimmed.Length > 100)
            throw new ArgumentException("AcademicLevel must be 2-100 characters.", nameof(value));
        Value = trimmed;
    }

    private AcademicLevel() => Value = string.Empty;

    public static AcademicLevel Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("AcademicLevel must not be empty.", nameof(value));

        var trimmed = value.Trim();
        if (trimmed.Length < 2 || trimmed.Length > 100)
            throw new ArgumentException("AcademicLevel must be 2-100 characters.", nameof(value));

        return new AcademicLevel(trimmed);
    }

    public static bool TryCreate(string value, out AcademicLevel? result, out string? error)
    {
        result = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value)) { error = "AcademicLevel must not be empty."; return false; }
        var trimmed = value.Trim();
        if (trimmed.Length < 2 || trimmed.Length > 100) { error = "AcademicLevel must be 2-100."; return false; }
        result = new AcademicLevel(trimmed);
        return true;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value.ToLowerInvariant();
    }

    public override string ToString() => Value;

    public static implicit operator string(AcademicLevel level) => level.Value;
}
