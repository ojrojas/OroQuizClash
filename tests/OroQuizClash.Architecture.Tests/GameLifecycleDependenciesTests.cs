using System.Reflection;

namespace OroQuizClash.Architecture.Tests;

public sealed class GameLifecycleDependenciesTests
{
    [Fact]
    public void Game_ShouldNotReferenceInfrastructureOrWeb()
    {
        var domainAssembly = typeof(OroQuizClash.Domain.Games.Game).Assembly;
        var referenced = domainAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        var forbidden = new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "RabbitMQ.Client", "MediatR", "MassTransit", "AutoMapper" };
        foreach (var f in forbidden)
        {
            Assert.DoesNotContain(referenced, r => r != null && r.StartsWith(f, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Game_ShouldBeAggregateRoot()
    {
        var type = typeof(OroQuizClash.Domain.Games.Game);
        Assert.True(typeof(BuildingBlocks.Kernel.Domain.Entities.AggregateRoot<OroQuizClash.Domain.Games.GameId>).IsAssignableFrom(type));
    }
}
