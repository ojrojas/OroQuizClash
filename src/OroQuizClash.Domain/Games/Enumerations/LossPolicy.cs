using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Games.Enumerations;

public sealed class LossPolicy(int id, string name) : Enumeration<LossPolicy>(id, name)
{
    public static readonly LossPolicy LoseAll = new(1, "LOSE_ALL");
    public static readonly LossPolicy LoseCurrentRound = new(2, "LOSE_CURRENT_ROUND");
    public static readonly LossPolicy LoseUnsecuredPoints = new(3, "LOSE_UNSECURED_POINTS");
    public static readonly LossPolicy FallbackToCheckpoint = new(4, "FALLBACK_TO_CHECKPOINT");
}