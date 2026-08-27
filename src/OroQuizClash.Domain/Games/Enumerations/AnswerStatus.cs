using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Games.Enumerations;

public sealed class AnswerStatus(int id, string name) : Enumeration<AnswerStatus>(id, name)
{
    public static readonly AnswerStatus NotAnswered = new(1, "NOT_ANSWERED");
    public static readonly AnswerStatus Answered = new(2, "ANSWERED");
    public static readonly AnswerStatus Evaluated = new(3, "EVALUATED");
    public static readonly AnswerStatus Expired = new(4, "EXPIRED");

    public bool IsTerminal => this == Evaluated || this == Expired;

    public bool IsInternal => this == Answered;

    public bool CanTransitionTo(AnswerStatus target)
    {
        return IsValidTransition(this, target);
    }

    public static bool IsValidTransition(AnswerStatus from, AnswerStatus to)
    {
        if (from == NotAnswered && (to == Answered || to == Expired)) return true;
        if (from == Answered && to == Evaluated) return true;
        return false;
    }
}
