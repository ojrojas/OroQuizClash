using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Questions.ValueObjects;

public sealed class AgeRange : ValueObject
{
    public int Min { get; }
    public int Max { get; }

    public AgeRange(int min, int max)
    {
        if (min < 0 || min > 120)
            throw new ArgumentOutOfRangeException(nameof(min), "Min must be 0-120.");
        if (max < 0 || max > 120)
            throw new ArgumentOutOfRangeException(nameof(max), "Max must be 0-120.");
        if (min > max)
            throw new ArgumentException("Min must be <= Max.");

        Min = min;
        Max = max;
    }

    private AgeRange()
    {
        Min = 0;
        Max = 0;
    }

    public static AgeRange Create(int min, int max)
    {
        if (min < 0 || min > 120)
            throw new ArgumentOutOfRangeException(nameof(min), "Min must be 0-120.");
        if (max < 0 || max > 120)
            throw new ArgumentOutOfRangeException(nameof(max), "Max must be 0-120.");
        if (min > max)
            throw new ArgumentException("Min must be <= Max.");

        return new AgeRange(min, max);
    }

    public static bool TryCreate(int min, int max, out AgeRange? result, out string? error)
    {
        result = null;
        error = null;
        if (min < 0 || min > 120) { error = "Min must be 0-120."; return false; }
        if (max < 0 || max > 120) { error = "Max must be 0-120."; return false; }
        if (min > max) { error = "Min must be <= Max."; return false; }
        result = new AgeRange(min, max);
        return true;
    }

    /// <summary>Checks if this range overlaps with another (compatible for QST-007 counting).</summary>
    public bool IsCompatible(AgeRange other) => Max >= other.Min && Min <= other.Max;

    public bool Contains(int age) => age >= Min && age <= Max;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Min;
        yield return Max;
    }

    public override string ToString() => $"{Min}-{Max}";
}
