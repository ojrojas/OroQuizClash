using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Categories.Rules;

public sealed class DifficultyLevelValidRule(int difficulty) : IBusinessRule
{
    public bool IsBroken() => difficulty < 1 || difficulty > 5;
    public string Message => "Difficulty level must be between 1 and 5.";
}