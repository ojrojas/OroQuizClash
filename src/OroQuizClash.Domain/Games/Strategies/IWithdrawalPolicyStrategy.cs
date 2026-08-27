using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;

namespace OroQuizClash.Domain.Games.Strategies;

public interface IWithdrawalPolicyStrategy
{
    int CalculateDeduction(PlayerScore score);
    string Name { get; }
}

public sealed class WithdrawLoseAllStrategy : IWithdrawalPolicyStrategy
{
    public string Name => WithdrawalPolicy.LoseAll.Name;
    public int CalculateDeduction(PlayerScore score) => score.CurrentPoints;
}

public sealed class WithdrawKeepCurrentStrategy : IWithdrawalPolicyStrategy
{
    public string Name => WithdrawalPolicy.KeepCurrentScore.Name;
    public int CalculateDeduction(PlayerScore score) => 0;
}

public sealed class WithdrawKeepSecuredStrategy : IWithdrawalPolicyStrategy
{
    public string Name => WithdrawalPolicy.KeepSecuredScore.Name;
    public int CalculateDeduction(PlayerScore score) => score.CurrentPoints - score.SecuredPoints;
}

public sealed class WithdrawKeepCheckpointStrategy : IWithdrawalPolicyStrategy
{
    public string Name => WithdrawalPolicy.KeepCheckpointScore.Name;
    public int CalculateDeduction(PlayerScore score) => score.CurrentPoints - score.SecuredPoints;
}

public static class WithdrawalPolicyStrategyFactory
{
    public static IWithdrawalPolicyStrategy Resolve(WithdrawalPolicy policy) => policy switch
    {
        var p when p == WithdrawalPolicy.LoseAll => new WithdrawLoseAllStrategy(),
        var p when p == WithdrawalPolicy.KeepCurrentScore => new WithdrawKeepCurrentStrategy(),
        var p when p == WithdrawalPolicy.KeepSecuredScore => new WithdrawKeepSecuredStrategy(),
        var p when p == WithdrawalPolicy.KeepCheckpointScore => new WithdrawKeepCheckpointStrategy(),
        _ => new WithdrawLoseAllStrategy()
    };
}
