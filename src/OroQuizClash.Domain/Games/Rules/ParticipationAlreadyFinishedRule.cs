using BuildingBlocks.Kernel.Domain.Rules;

using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class ParticipationAlreadyFinishedRule(PlayerParticipationStatus status) : IBusinessRule
{
    public bool IsBroken() => status != PlayerParticipationStatus.Active;
    public string Message => "Player participation has already finished.";
}
