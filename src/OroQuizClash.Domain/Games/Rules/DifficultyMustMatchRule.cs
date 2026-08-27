using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class DifficultyMustMatchRule(int expectedDifficulty, int actualDifficulty) : IBusinessRule
{
    public bool IsBroken() => expectedDifficulty != actualDifficulty;
    public string Message => $"Question difficulty {actualDifficulty} does not match the expected difficulty {expectedDifficulty}.";
}
