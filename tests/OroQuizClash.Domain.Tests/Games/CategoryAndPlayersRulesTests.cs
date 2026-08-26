using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Rules;
using OroQuizClash.Domain.Games.ValueObjects;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class CategoryAndPlayersRulesTests
{
    [Fact]
    public void PlayersRange_MinGreaterThanMax_IsBroken()
    {
        var rule = new PlayersRangeCoherenceRule(5, 2);
        Assert.True(rule.IsBroken());
    }

    [Fact]
    public void PlayersRange_MinZero_IsBroken()
    {
        var rule = new PlayersRangeCoherenceRule(0, 10);
        Assert.True(rule.IsBroken());
    }

    [Fact]
    public void TimeRange_Over300_IsBroken()
    {
        var rule = new TimeLimitRangeRule(301);
        Assert.True(rule.IsBroken());
    }
}