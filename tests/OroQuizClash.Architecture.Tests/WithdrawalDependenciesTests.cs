using System.Reflection;

namespace OroQuizClash.Architecture.Tests;

public sealed class WithdrawalDependenciesTests
{
    [Fact]
    public void PlayerParticipationStatus_ShouldBeEnumeration()
    {
        var type = typeof(OroQuizClash.Domain.Games.Enumerations.PlayerParticipationStatus);
        Assert.True(typeof(BuildingBlocks.Kernel.Domain.Enumerations.Enumeration<OroQuizClash.Domain.Games.Enumerations.PlayerParticipationStatus>).IsAssignableFrom(type));
    }

    [Fact]
    public void PlayerParticipationStatus_ShouldHaveFourStates()
    {
        var all = OroQuizClash.Domain.Games.Enumerations.PlayerParticipationStatus.GetAll();
        Assert.Equal(4, all.Count);
    }

    [Fact]
    public void GamePlayer_ShouldNotExposePublicSetters()
    {
        var type = typeof(OroQuizClash.Domain.Games.GamePlayer);
        var mutableProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .ToList();
        Assert.Empty(mutableProps);
    }

    [Fact]
    public void Game_ShouldExposeWithdrawalOperations()
    {
        var type = typeof(OroQuizClash.Domain.Games.Game);
        Assert.NotNull(type.GetMethod("WithdrawPlayer", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(type.GetMethod("EliminatePlayer", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void GamePlayer_ShouldHaveParticipationStatus()
    {
        var type = typeof(OroQuizClash.Domain.Games.GamePlayer);
        var prop = type.GetProperty("ParticipationStatus");
        Assert.NotNull(prop);
        Assert.Equal(typeof(OroQuizClash.Domain.Games.Enumerations.PlayerParticipationStatus), prop!.PropertyType);
    }

    [Fact]
    public void Domain_ShouldNotReferenceInfrastructureOrWeb()
    {
        var domainAssembly = typeof(OroQuizClash.Domain.Games.GamePlayer).Assembly;
        var referenced = domainAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        var forbidden = new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "RabbitMQ.Client", "MediatR", "MassTransit", "AutoMapper" };
        foreach (var f in forbidden)
        {
            Assert.DoesNotContain(referenced, r => r != null && r.StartsWith(f, StringComparison.OrdinalIgnoreCase));
        }
    }
}
