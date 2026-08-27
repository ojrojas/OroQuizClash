using BuildingBlocks.Kernel.Domain.Rules;

using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class PlayerAlreadyEliminatedRule(PlayerParticipationStatus status) : IBusinessRule
{
    public bool IsBroken() => status == PlayerParticipationStatus.Eliminated;
    public string Message => "Player has been eliminated and cannot withdraw.";
}
