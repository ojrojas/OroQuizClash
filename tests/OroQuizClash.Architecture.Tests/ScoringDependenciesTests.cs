using System.Reflection;

namespace OroQuizClash.Architecture.Tests;

public sealed class ScoringDependenciesTests
{
    [Fact]
    public void Domain_ShouldNotReferenceInfrastructureOrWeb()
    {
        var domainAssembly = typeof(OroQuizClash.Domain.Games.PointTransaction).Assembly;
        var referenced = domainAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        var forbidden = new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "RabbitMQ.Client", "MediatR", "MassTransit", "AutoMapper" };
        foreach (var f in forbidden)
        {
            Assert.DoesNotContain(referenced, r => r != null && r.StartsWith(f, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void PointTransaction_ShouldBeEntity()
    {
        var type = typeof(OroQuizClash.Domain.Games.PointTransaction);
        Assert.True(typeof(BuildingBlocks.Kernel.Domain.Entities.Entity<OroQuizClash.Domain.Games.PointTransactionId>).IsAssignableFrom(type));
    }

    [Fact]
    public void PointTransaction_ShouldBeAppendOnly_NoPublicSetters()
    {
        var type = typeof(OroQuizClash.Domain.Games.PointTransaction);
        var mutableProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .ToList();
        Assert.Empty(mutableProps);
    }

    [Fact]
    public void PointTransactionType_ShouldHaveTenTypes()
    {
        var all = OroQuizClash.Domain.Games.Enumerations.PointTransactionType.GetAll();
        Assert.Equal(10, all.Count);
    }

    [Fact]
    public void PlayerScore_ShouldBeValueObject()
    {
        var type = typeof(OroQuizClash.Domain.Games.ValueObjects.PlayerScore);
        Assert.True(typeof(BuildingBlocks.Kernel.Domain.ValueObjects.ValueObject).IsAssignableFrom(type));
    }

    [Fact]
    public void LossPolicyStrategies_ShouldImplementInterface()
    {
        var strategyTypes = new[]
        {
            typeof(OroQuizClash.Domain.Games.Strategies.LoseAllStrategy),
            typeof(OroQuizClash.Domain.Games.Strategies.LoseCurrentRoundStrategy),
            typeof(OroQuizClash.Domain.Games.Strategies.LoseUnsecuredPointsStrategy),
            typeof(OroQuizClash.Domain.Games.Strategies.FallbackToCheckpointStrategy),
        };
        foreach (var type in strategyTypes)
        {
            Assert.True(typeof(OroQuizClash.Domain.Games.Strategies.ILossPolicyStrategy).IsAssignableFrom(type));
        }
    }

    [Fact]
    public void WithdrawalPolicyStrategies_ShouldImplementInterface()
    {
        var strategyTypes = new[]
        {
            typeof(OroQuizClash.Domain.Games.Strategies.WithdrawLoseAllStrategy),
            typeof(OroQuizClash.Domain.Games.Strategies.WithdrawKeepCurrentStrategy),
            typeof(OroQuizClash.Domain.Games.Strategies.WithdrawKeepSecuredStrategy),
            typeof(OroQuizClash.Domain.Games.Strategies.WithdrawKeepCheckpointStrategy),
        };
        foreach (var type in strategyTypes)
        {
            Assert.True(typeof(OroQuizClash.Domain.Games.Strategies.IWithdrawalPolicyStrategy).IsAssignableFrom(type));
        }
    }

    [Fact]
    public void Game_ShouldExposeScoringOperations()
    {
        var type = typeof(OroQuizClash.Domain.Games.Game);
        var operations = new[] { "AwardPoints", "RemovePoints", "SecurePoints", "ConsumePoints", "WithdrawPlayer", "AdjustPoints" };
        foreach (var op in operations)
        {
            Assert.NotNull(type.GetMethod(op, BindingFlags.Public | BindingFlags.Instance));
        }
    }
}
