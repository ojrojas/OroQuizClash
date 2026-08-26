using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Games.Enumerations;

public sealed class ConsolationPolicy(int id, string name) : Enumeration<ConsolationPolicy>(id, name)
{
    public static readonly ConsolationPolicy None = new(1, "None");
    public static readonly ConsolationPolicy FixedPoints = new(2, "FixedPoints");
    public static readonly ConsolationPolicy Badge = new(3, "Badge");
}