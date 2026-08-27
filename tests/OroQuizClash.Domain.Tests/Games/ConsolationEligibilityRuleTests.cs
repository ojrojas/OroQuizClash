using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Rules;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class ConsolationEligibilityRuleTests
{
    [Fact]
    public void Eligible_ActiveNonWinner_MeetsThresholds()
    {
        var rule = new ConsolationEligibilityRule(
            isEliminated: false, isWinner: false,
            playerParticipationRounds: 3, playerAnsweredQuestions: 5,
            minimumParticipationRounds: 2, minimumAnsweredQuestions: 3,
            policy: ConsolationPolicy.FixedPoints);

        Assert.False(rule.IsBroken());
    }

    [Fact]
    public void Ineligible_Winner()
    {
        var rule = new ConsolationEligibilityRule(
            isEliminated: false, isWinner: true,
            playerParticipationRounds: 5, playerAnsweredQuestions: 10,
            minimumParticipationRounds: 2, minimumAnsweredQuestions: 3,
            policy: ConsolationPolicy.FixedPoints);

        Assert.True(rule.IsBroken());
    }

    [Fact]
    public void Ineligible_BelowMinRounds()
    {
        var rule = new ConsolationEligibilityRule(
            isEliminated: false, isWinner: false,
            playerParticipationRounds: 1, playerAnsweredQuestions: 5,
            minimumParticipationRounds: 3, minimumAnsweredQuestions: 3,
            policy: ConsolationPolicy.FixedPoints);

        Assert.True(rule.IsBroken());
    }

    [Fact]
    public void Ineligible_BelowMinAnswered()
    {
        var rule = new ConsolationEligibilityRule(
            isEliminated: false, isWinner: false,
            playerParticipationRounds: 5, playerAnsweredQuestions: 1,
            minimumParticipationRounds: 2, minimumAnsweredQuestions: 3,
            policy: ConsolationPolicy.FixedPoints);

        Assert.True(rule.IsBroken());
    }

    [Fact]
    public void Ineligible_Eliminated()
    {
        var rule = new ConsolationEligibilityRule(
            isEliminated: true, isWinner: false,
            playerParticipationRounds: 5, playerAnsweredQuestions: 10,
            minimumParticipationRounds: 2, minimumAnsweredQuestions: 3,
            policy: ConsolationPolicy.FixedPoints);

        Assert.True(rule.IsBroken());
    }

    [Fact]
    public void Ineligible_PolicyNone()
    {
        var rule = new ConsolationEligibilityRule(
            isEliminated: false, isWinner: false,
            playerParticipationRounds: 5, playerAnsweredQuestions: 10,
            minimumParticipationRounds: 0, minimumAnsweredQuestions: 0,
            policy: ConsolationPolicy.None);

        Assert.True(rule.IsBroken());
    }

    [Fact]
    public void Eligible_WithdrawnButMeetsThresholds()
    {
        var rule = new ConsolationEligibilityRule(
            isEliminated: false, isWinner: false,
            playerParticipationRounds: 3, playerAnsweredQuestions: 5,
            minimumParticipationRounds: 2, minimumAnsweredQuestions: 3,
            policy: ConsolationPolicy.FixedPoints);

        Assert.False(rule.IsBroken());
    }

    [Fact]
    public void Eligible_ZeroMinimums()
    {
        var rule = new ConsolationEligibilityRule(
            isEliminated: false, isWinner: false,
            playerParticipationRounds: 0, playerAnsweredQuestions: 0,
            minimumParticipationRounds: 0, minimumAnsweredQuestions: 0,
            policy: ConsolationPolicy.FixedPoints);

        Assert.False(rule.IsBroken());
    }
}
