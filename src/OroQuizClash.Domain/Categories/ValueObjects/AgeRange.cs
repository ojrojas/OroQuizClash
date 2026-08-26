using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Categories.ValueObjects;

public sealed class AgeRange : ValueObject
{
    public int Min { get; }

    public int Max { get; }

    public AgeRange(int min, int max)
    {
        if (min < 0 || min > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(min), min, "Min must be between 0 and 120.");
        }

        if (max < 0 || max > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(max), max, "Max must be between 0 and 120.");
        }

        if (min > max)
        {
            throw new ArgumentException("Min must be <= Max.", nameof(min));
        }

        Min = min;
        Max = max;
    }

    private AgeRange()
    {
        Min = 0;
        Max = 0;
    }

    public bool Contains(int age) => age >= Min && age <= Max;

    /// <summary>Checks whether two age ranges overlap (compatible).</summary>
    public bool IsCompatible(AgeRange other) => Max >= other.Min && Min <= other.Max;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Min;
        yield return Max;
    }
}