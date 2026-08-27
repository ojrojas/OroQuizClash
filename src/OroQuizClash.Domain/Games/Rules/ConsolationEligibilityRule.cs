using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class ConsolationEligibilityRule(
    bool isEliminated,
    bool isWinner,
    int playerParticipationRounds,
    int playerAnsweredQuestions,
    int minimumParticipationRounds,
    int minimumAnsweredQuestions,
    Enumerations.ConsolationPolicy policy) : IBusinessRule
{
    public bool IsBroken()
    {
        if (policy == Enumerations.ConsolationPolicy.None)
            return true;

        if (isEliminated)
            return true;

        if (isWinner)
            return true;

        if (playerParticipationRounds < minimumParticipationRounds)
            return true;

        if (playerAnsweredQuestions < minimumAnsweredQuestions)
            return true;

        return false;
    }

    public string Message => "Player is not eligible for consolation.";
}
