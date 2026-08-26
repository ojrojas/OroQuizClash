using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Games.Enumerations;

public sealed class DifficultyProgressionStrategy(int id, string name) : Enumeration<DifficultyProgressionStrategy>(id, name)
{
    public static readonly DifficultyProgressionStrategy Linear = new(1, "Linear");
    public static readonly DifficultyProgressionStrategy Progressive = new(2, "Progressive");
    public static readonly DifficultyProgressionStrategy Adaptive = new(3, "Adaptive");
    public static readonly DifficultyProgressionStrategy CategorySpecific = new(4, "CategorySpecific");
}