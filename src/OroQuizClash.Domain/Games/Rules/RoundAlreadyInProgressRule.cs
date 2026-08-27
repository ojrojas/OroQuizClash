using BuildingBlocks.Kernel.Domain.Rules;

using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class RoundAlreadyInProgressRule : IBusinessRule
{
    private readonly GameStatus _status;
    private readonly GameRound? _currentRound;

    public RoundAlreadyInProgressRule(GameStatus status, GameRound? currentRound)
    {
        _status = status;
        _currentRound = currentRound;
    }

    public bool IsBroken() => _status == GameStatus.RoundInProgress || _currentRound != null;
    public string Message => "A round is already in progress.";
}
