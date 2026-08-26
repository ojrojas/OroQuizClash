using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Categories.ValueObjects;

public sealed class DifficultyLevel : ValueObject
{
    public int Value { get; }

    public DifficultyLevel(int value)
    {
        if (value < 1 || value > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "DifficultyLevel must be between 1 and 5.");
        }

        Value = value;
    }

    private DifficultyLevel()
    {
        Value = 1;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}