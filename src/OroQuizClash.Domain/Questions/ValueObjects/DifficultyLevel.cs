using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Questions.ValueObjects;

public sealed class DifficultyLevel(int id, string name) : Enumeration<DifficultyLevel>(id, name)
{
    public static readonly DifficultyLevel Basic = new(1, "Basic");
    public static readonly DifficultyLevel Elementary = new(2, "Elementary");
    public static readonly DifficultyLevel Intermediate = new(3, "Intermediate");
    public static readonly DifficultyLevel Advanced = new(4, "Advanced");
    public static readonly DifficultyLevel Expert = new(5, "Expert");
}
