using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Games.ValueObjects;

public sealed class RewardRules : ValueObject
{
    public string Type { get; }
    public int Threshold { get; }

    public RewardRules(string type, int threshold)
    {
        Type = type;
        Threshold = threshold;
    }

    private RewardRules() { Type = string.Empty; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return Threshold;
    }
}