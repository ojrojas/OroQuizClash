using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class PlayersRangeCoherenceRule(int minPlayers, int maxPlayers) : IBusinessRule
{
    public bool IsBroken() => minPlayers < 1 || maxPlayers < 1 || minPlayers > maxPlayers;
    public string Message => "Players range invalid: min >=1 and min <= max.";
}