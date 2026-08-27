using System.Reflection;

namespace OroQuizClash.Architecture.Tests;

public sealed class ConsolidationDependenciesTests
{
    [Fact]
    public void ConsolationPolicy_ShouldHaveFourValues()
    {
        var all = OroQuizClash.Domain.Games.Enumerations.ConsolationPolicy.GetAll();
        Assert.Equal(4, all.Count);
    }

    [Fact]
    public void ConsolationEligibilityRule_ShouldImplementIBusinessRule()
    {
        var type = typeof(OroQuizClash.Domain.Games.Rules.ConsolationEligibilityRule);
        Assert.True(typeof(BuildingBlocks.Kernel.Domain.Rules.IBusinessRule).IsAssignableFrom(type));
    }

    [Fact]
    public void Game_ShouldExposeFinishMethod()
    {
        var type = typeof(OroQuizClash.Domain.Games.Game);
        Assert.NotNull(type.GetMethod("Finish", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void GetPlayerConsolationStatus_ShouldImplementIQueryHandler()
    {
        var type = typeof(OroQuizClash.Application.Features.Games.GetPlayerConsolationStatusHandler);
        var interfaces = type.GetInterfaces();
        Assert.Contains(interfaces, i => i.Name.Contains("IQueryHandler"));
    }

    [Fact]
    public void GetPlayerConsolationHistory_ShouldImplementIQueryHandler()
    {
        var type = typeof(OroQuizClash.Application.Features.Games.GetPlayerConsolationHistoryHandler);
        var interfaces = type.GetInterfaces();
        Assert.Contains(interfaces, i => i.Name.Contains("IQueryHandler"));
    }

    [Fact]
    public void ConsolidationSlices_ShouldNotUseMediatR()
    {
        var assembly = typeof(OroQuizClash.Application.Features.Games.GetPlayerConsolationStatusHandler).Assembly;
        var types = assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("OroQuizClash.Application.Features.Games") == true &&
                        (t.Name.Contains("Consolidation") || t.Name.Contains("Consolation")));

        foreach (var type in types)
        {
            var references = type.Assembly.GetReferencedAssemblies();
            Assert.DoesNotContain(references, a => a.Name == "MediatR");
            Assert.DoesNotContain(references, a => a.Name == "AutoMapper");
        }
    }

    [Fact]
    public void GameConfiguration_ShouldHaveConsolationFields()
    {
        var type = typeof(OroQuizClash.Domain.Games.ValueObjects.GameConfiguration);
        Assert.NotNull(type.GetProperty("MinimumParticipationRounds"));
        Assert.NotNull(type.GetProperty("MinimumAnsweredQuestions"));
        Assert.NotNull(type.GetProperty("ConsolationPoints"));
        Assert.NotNull(type.GetProperty("ConsolationRewardId"));
    }
}
