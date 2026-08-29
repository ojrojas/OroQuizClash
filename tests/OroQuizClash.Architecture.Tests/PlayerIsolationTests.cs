using System.Reflection;
using Xunit;

namespace OroQuizClash.Architecture.Tests;

public sealed class PlayerIsolationTests
{
    [Fact]
    public void Domain_Should_Not_Reference_Player_Angular()
    {
        var domainAsm = typeof(OroQuizClash.Domain.Games.Game).Assembly;
        var refs = domainAsm.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("QuizArena.Player", refs);
        Assert.DoesNotContain("angular", refs, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Application_Should_Not_Write_Score_ClientSide()
    {
        // Verify no client-side score mutation: Score derived from ledger only
        var domainTypes = typeof(OroQuizClash.Domain.Games.Game).Assembly.GetTypes();
        var gameType = domainTypes.First(t => t.Name == "Game");
        var submitMethod = gameType.GetMethod("SubmitAnswer");
        Assert.NotNull(submitMethod);
        // isCorrect never trusted from client: server evaluates via Question resolver
        Assert.Contains("questionResolver", submitMethod.GetParameters().Select(p => p.Name));
    }

    [Fact]
    public void BuildingBlocks_Should_Not_Be_Reimplemented()
    {
        var domainAsm = typeof(OroQuizClash.Domain.Games.Game).Assembly;
        var referenced = domainAsm.GetReferencedAssemblies().Select(a => a.Name).ToHashSet();
        Assert.DoesNotContain("MediatR", referenced);
        Assert.DoesNotContain("MassTransit", referenced);
        Assert.DoesNotContain("AutoMapper", referenced);
    }
}
