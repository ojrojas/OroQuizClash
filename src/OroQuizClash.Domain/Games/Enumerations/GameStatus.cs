using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Games.Enumerations;

public sealed class GameStatus(int id, string name) : Enumeration<GameStatus>(id, name)
{
    public static readonly GameStatus Draft = new(1, "DRAFT");
    public static readonly GameStatus Ready = new(2, "READY");
    public static readonly GameStatus WaitingForPlayers = new(3, "WAITING_FOR_PLAYERS");
    public static readonly GameStatus InProgress = new(4, "IN_PROGRESS");
    public static readonly GameStatus RoundInProgress = new(5, "ROUND_IN_PROGRESS");
    public static readonly GameStatus RoundCompleted = new(6, "ROUND_COMPLETED");
    public static readonly GameStatus Finished = new(7, "FINISHED");
    public static readonly GameStatus Cancelled = new(8, "CANCELLED");
    public static readonly GameStatus ForcedFinished = new(9, "FORCED_FINISHED");

    public bool IsTerminal => this == Finished || this == Cancelled || this == ForcedFinished;

    public bool IsStarted => Id >= WaitingForPlayers.Id;

    public bool IsRoundActive => this == RoundInProgress;

    public bool CanTransitionTo(GameStatus target)
    {
        return IsValidTransition(this, target);
    }

    public static bool IsValidTransition(GameStatus from, GameStatus to)
    {
        if (from == Draft && (to == Ready || to == Cancelled)) return true;
        if (from == Ready && (to == WaitingForPlayers || to == Cancelled)) return true;
        if (from == WaitingForPlayers && (to == InProgress || to == Cancelled)) return true;
        if (from == InProgress && (to == RoundInProgress || to == Finished || to == Cancelled || to == ForcedFinished)) return true;
        if (from == RoundInProgress && (to == RoundCompleted || to == Cancelled || to == ForcedFinished || to == Finished)) return true;
        if (from == RoundCompleted && (to == RoundInProgress || to == Finished || to == Cancelled || to == ForcedFinished)) return true;
        // Terminal has no outgoing
        return false;
    }
}