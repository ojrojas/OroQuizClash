using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;

namespace OroQuizClash.Domain.Games.Strategies;

public interface ILossPolicyStrategy
{
    int CalculateDeduction(PlayerScore score);
    string Name { get; }
}

public sealed class LoseAllStrategy : ILossPolicyStrategy
{
    public string Name => LossPolicy.LoseAll.Name;
    public int CalculateDeduction(PlayerScore score) => score.CurrentPoints;
}

public sealed class LoseCurrentRoundStrategy : ILossPolicyStrategy
{
    public string Name => LossPolicy.LoseCurrentRound.Name;
    public int CalculateDeduction(PlayerScore score) => score.RoundPoints;
}

public sealed class LoseUnsecuredPointsStrategy : ILossPolicyStrategy
{
    public string Name => LossPolicy.LoseUnsecuredPoints.Name;
    public int CalculateDeduction(PlayerScore score) => score.CurrentPoints - score.SecuredPoints;
}

public sealed class FallbackToCheckpointStrategy : ILossPolicyStrategy
{
    public string Name => LossPolicy.FallbackToCheckpoint.Name;
    public int CalculateDeduction(PlayerScore score) => score.CurrentPoints - score.SecuredPoints;
}

public static class LossPolicyStrategyFactory
{
    public static ILossPolicyStrategy Resolve(LossPolicy policy) => policy switch
    {
        var p when p == LossPolicy.LoseAll => new LoseAllStrategy(),
        var p when p == LossPolicy.LoseCurrentRound => new LoseCurrentRoundStrategy(),
        var p when p == LossPolicy.LoseUnsecuredPoints => new LoseUnsecuredPointsStrategy(),
        var p when p == LossPolicy.FallbackToCheckpoint => new FallbackToCheckpointStrategy(),
        _ => new LoseAllStrategy()
    };
}
