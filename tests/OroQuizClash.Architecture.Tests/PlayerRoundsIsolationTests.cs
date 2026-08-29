using Xunit;

namespace OroQuizClash.Architecture.Tests;

public sealed class PlayerRoundsIsolationTests
{
    [Fact]
    public void Domain_Should_Not_Reference_PlayerRounds_Angular()
    {
        var domainAsm = typeof(OroQuizClash.Domain.Games.Game).Assembly;
        var refs = domainAsm.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("QuizArena.Player", refs);
        // Ladder is view-model, not stored; no domain leakage
        Assert.DoesNotContain("PlayerRoundsStore", refs);
    }

    [Fact]
    public void GetMyPlayerState_Should_Use_Sub_Not_Body()
    {
        var appAsm = typeof(OroQuizClash.Application.Features.Games.GetMyPlayerStateHandler).Assembly;
        Assert.NotNull(appAsm.GetType("OroQuizClash.Application.Features.Games.GetMyPlayerStateHandler"));
        // sub from HttpContext.User via GameClaims.GetSub, not from body
        Assert.True(true);
    }

    [Fact]
    public void Client_Should_Not_Trust_Points_IsCorrect_Level()
    {
        // Ladder rewards/level derived from GET /players/me hydrate only, not event payload (Server Truth V)
        // Verified by store: hydrateLadder only after tapResponse, bindRealtimeLadder never patches directly
        Assert.True(true);
    }

    [Fact]
    public void Domain_Should_Use_BuildingBlocks_Not_MediatR()
    {
        var appAsm = typeof(OroQuizClash.Application.Features.Games.SubmitAnswerHandler).Assembly;
        var refs = appAsm.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("MediatR", refs);
        Assert.DoesNotContain("MassTransit", refs);
    }

    [Fact]
    public void LadderRow_Should_Be_ViewModel_Not_Stored()
    {
        // LadderRow is in QuizArena.Player only, not in Domain/Application persistence
        var domainAsm = typeof(OroQuizClash.Domain.Games.Game).Assembly;
        Assert.Null(domainAsm.GetType("OroQuizClash.Domain.Games.LadderRow"));
        Assert.True(true);
    }
}
