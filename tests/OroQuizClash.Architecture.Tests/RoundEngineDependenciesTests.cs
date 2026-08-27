using System.Reflection;

namespace OroQuizClash.Architecture.Tests;

public sealed class RoundEngineDependenciesTests
{
    [Fact]
    public void GameRound_ShouldNotReferenceInfrastructureOrWeb()
    {
        var domainAssembly = typeof(OroQuizClash.Domain.Games.GameRound).Assembly;
        var referenced = domainAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        var forbidden = new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "RabbitMQ.Client", "MediatR", "MassTransit", "AutoMapper" };
        foreach (var f in forbidden)
        {
            Assert.DoesNotContain(referenced, r => r != null && r.StartsWith(f, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void GameRound_ShouldBeEntity()
    {
        var type = typeof(OroQuizClash.Domain.Games.GameRound);
        Assert.True(typeof(BuildingBlocks.Kernel.Domain.Entities.Entity<OroQuizClash.Domain.Games.GameRoundId>).IsAssignableFrom(type));
    }

    [Fact]
    public void GameRound_ShouldReferenceQuestionDomain()
    {
        var type = typeof(OroQuizClash.Domain.Games.GameRound);
        var questionIdProp = type.GetProperty("QuestionId");
        Assert.NotNull(questionIdProp);
        Assert.Equal(typeof(OroQuizClash.Domain.Questions.QuestionId), questionIdProp!.PropertyType);
    }

    [Fact]
    public void DifficultyStrategies_ShouldImplementInterface()
    {
        var strategyTypes = new[]
        {
            typeof(OroQuizClash.Domain.Games.Strategies.LinearDifficultyStrategy),
            typeof(OroQuizClash.Domain.Games.Strategies.ProgressiveDifficultyStrategy),
            typeof(OroQuizClash.Domain.Games.Strategies.AdaptiveDifficultyStrategy),
        };
        foreach (var type in strategyTypes)
        {
            Assert.True(typeof(OroQuizClash.Domain.Games.Strategies.IDifficultyProgressionStrategy).IsAssignableFrom(type));
        }
    }
}
