using System.Reflection;

using OroQuizClash.Api.Hubs;
using OroQuizClash.Application.Features.Games;

namespace OroQuizClash.Architecture.Tests;

public sealed class RealtimeDependencyTests
{
    [Fact]
    public void GameHub_ShouldNotReferenceDomainDirectly()
    {
        var apiAssembly = typeof(GameHub).Assembly;
        var hubType = typeof(GameHub);
        var domainAssemblyName = typeof(OroQuizClash.Domain.Games.Game).Assembly.GetName().Name;

        var referencesDomainDirectly = hubType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
            .Any(t => t.Assembly.GetName().Name == domainAssemblyName);

        Assert.False(referencesDomainDirectly, "GameHub should not reference Domain types directly; use IRepository/Specification abstractions");

        var referencedAssemblies = apiAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        Assert.Contains(referencedAssemblies, n => n == typeof(IGameNotificationsBroadcaster).Assembly.GetName().Name);
    }

    [Fact]
    public void IGameNotificationsBroadcaster_ShouldLiveInApplicationLayer()
    {
        var port = typeof(IGameNotificationsBroadcaster);
        Assert.Equal("OroQuizClash.Application.Features.Games", port.Namespace);
        Assert.True(port.IsInterface);
        Assert.Contains(port.GetMethods(), m => m.Name == "GameStartedAsync");
        Assert.Contains(port.GetMethods(), m => m.Name == "RoundStartedAsync");
        Assert.Contains(port.GetMethods(), m => m.Name == "QuestionPresentedAsync");
        Assert.Contains(port.GetMethods(), m => m.Name == "PlayerAnsweredAsync");
        Assert.Contains(port.GetMethods(), m => m.Name == "RoundCompletedAsync");
        Assert.Contains(port.GetMethods(), m => m.Name == "GameFinishedAsync");
    }

    [Fact]
    public void GameHub_ShouldRemainBroadcastOnly()
    {
        var hubMethods = typeof(GameHub)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToList();
        Assert.Equal(["JoinGameGroup"], hubMethods);
    }
}
