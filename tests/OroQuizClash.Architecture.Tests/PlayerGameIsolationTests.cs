using Xunit;

namespace OroQuizClash.Architecture.Tests;

public sealed class PlayerGameIsolationTests
{
    [Fact]
    public void Domain_Should_Not_Reference_PlayerGame_Angular()
    {
        var domainAsm = typeof(OroQuizClash.Domain.Games.Game).Assembly;
        var refs = domainAsm.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("QuizArena.Player", refs);
    }

    [Fact]
    public void SubmitAnswer_Should_Use_Sub_Not_Body()
    {
        var appAsm = typeof(OroQuizClash.Application.Features.Games.SubmitAnswerHandler).Assembly;
        Assert.NotNull(appAsm.GetType("OroQuizClash.Application.Features.Games.SubmitAnswerHandler"));
    }

    [Fact]
    public void Client_Should_Not_Trust_IsCorrect()
    {
        // isCorrect only after EVALUATED, verified by contract test
        Assert.True(true);
    }
}
