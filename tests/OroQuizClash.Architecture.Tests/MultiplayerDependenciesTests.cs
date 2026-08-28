using System.Reflection;

using OroQuizClash.Api.Hubs;
using OroQuizClash.Application.Features.Games;

namespace OroQuizClash.Architecture.Tests;

public sealed class MultiplayerDependenciesTests
{
    private static readonly string[] ForbiddenDomainReferences =
        ["Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "RabbitMQ.Client", "MediatR", "MassTransit", "AutoMapper"];

    [Fact]
    public void MultiplayerDomain_ShouldNotReferenceInfrastructureOrWeb()
    {
        var domainAssembly = typeof(OroQuizClash.Domain.Games.GamePlayer).Assembly;
        var referenced = domainAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        foreach (var forbidden in ForbiddenDomainReferences)
        {
            Assert.DoesNotContain(referenced, r => r != null && r.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void GamePlayer_ShouldNotExposePublicSetters()
    {
        var mutableProps = typeof(OroQuizClash.Domain.Games.GamePlayer)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .ToList();
        Assert.Empty(mutableProps);
    }

    [Fact]
    public void GamePlayer_ShouldExposeCurrentRoundNumber()
    {
        var prop = typeof(OroQuizClash.Domain.Games.GamePlayer).GetProperty("CurrentRoundNumber");
        Assert.NotNull(prop);
        Assert.Equal(typeof(int), prop!.PropertyType);
        Assert.False(prop.SetMethod?.IsPublic ?? false);
    }

    [Fact]
    public void NotificationsPort_ShouldNotExposeSignalROrAspNetCoreTypes()
    {
        var port = typeof(IGameNotificationsBroadcaster);
        Assert.True(port.IsInterface);

        foreach (var method in port.GetMethods())
        {
            var types = method.GetParameters().Select(p => p.ParameterType)
                .Append(method.ReturnType)
                .SelectMany(t => t.IsGenericType ? t.GetGenericArguments() : [t]);

            foreach (var type in types)
            {
                var ns = type.Namespace ?? string.Empty;
                Assert.False(ns.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase),
                    $"Port method {method.Name} leaks ASP.NET Core type {type.FullName}");
                Assert.False(ns.StartsWith("Microsoft.AspNetCore.SignalR", StringComparison.OrdinalIgnoreCase),
                    $"Port method {method.Name} leaks SignalR type {type.FullName}");
            }
        }
    }

    [Fact]
    public void SignalRBroadcaster_ShouldImplementNotificationsPort()
    {
        Assert.True(typeof(IGameNotificationsBroadcaster).IsAssignableFrom(typeof(SignalRGameNotificationsBroadcaster)));
    }

    [Fact]
    public void GameHub_ShouldBeBroadcastOnly_NoGameCommandMethods()
    {
        var hubMethods = typeof(GameHub)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToList();

        Assert.Equal(["JoinGameGroup"], hubMethods);
    }
}
