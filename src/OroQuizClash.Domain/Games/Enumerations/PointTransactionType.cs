using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Games.Enumerations;

public sealed class PointTransactionType(int id, string name) : Enumeration<PointTransactionType>(id, name)
{
    public static readonly PointTransactionType AnswerCorrect = new(1, "ANSWER_CORRECT");
    public static readonly PointTransactionType AnswerIncorrect = new(2, "ANSWER_INCORRECT");
    public static readonly PointTransactionType RoundBonus = new(3, "ROUND_BONUS");
    public static readonly PointTransactionType LevelBonus = new(4, "LEVEL_BONUS");
}
