using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Games.Enumerations;

public sealed class PlayerParticipationStatus(int id, string name) : Enumeration<PlayerParticipationStatus>(id, name)
{
    public static readonly PlayerParticipationStatus Active = new(1, "ACTIVE");
    public static readonly PlayerParticipationStatus Withdrawn = new(2, "WITHDRAWN");
    public static readonly PlayerParticipationStatus Eliminated = new(3, "ELIMINATED");
    public static readonly PlayerParticipationStatus Winner = new(4, "WINNER");

    public bool IsTerminalParticipation =>
        this == Withdrawn || this == Eliminated || this == Winner;
}
