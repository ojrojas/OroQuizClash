using BuildingBlocks.Kernel.Domain.Rules;

using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class DifficultyStrategyRequiredRule(DifficultyProgressionStrategy? strategy) : IBusinessRule
{
    public bool IsBroken() => strategy is null;
    public string Message => "Difficulty strategy is required.";
}