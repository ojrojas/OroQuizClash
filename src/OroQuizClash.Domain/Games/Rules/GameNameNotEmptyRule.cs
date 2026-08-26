using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class GameNameNotEmptyRule(string name) : IBusinessRule
{
    public bool IsBroken() => string.IsNullOrWhiteSpace(name) || name.Trim().Length < 3 || name.Trim().Length > 100;
    public string Message => "Game name must be 3-100 characters and not empty.";
}