using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Games.Enumerations;

public sealed class ScoringSystem(int id, string name) : Enumeration<ScoringSystem>(id, name)
{
    public static readonly ScoringSystem Standard = new(1, "Standard");
    public static readonly ScoringSystem ProgressiveBonus = new(2, "ProgressiveBonus");
}