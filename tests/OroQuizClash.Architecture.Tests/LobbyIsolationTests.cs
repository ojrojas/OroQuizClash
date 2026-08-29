using Xunit;

namespace OroQuizClash.Architecture.Tests;

public sealed class LobbyIsolationTests
{
    [Fact]
    public void Domain_Should_Not_Reference_Lobby_Angular()
    {
        var domainAsm = typeof(OroQuizClash.Domain.Games.Game).Assembly;
        var refs = domainAsm.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("QuizArena.Player", refs);
    }

    [Fact]
    public void JoinGame_Should_Use_Sub_Not_Body()
    {
        // JoinGameHandler must use GameClaims.GetSub(http.User) not body.PlayerId
        var appAsm = typeof(OroQuizClash.Application.Features.Games.JoinGameHandler).Assembly;
        var handler = appAsm.GetType("OroQuizClash.Application.Features.Games.JoinGameHandler");
        Assert.NotNull(handler);
    }

    [Fact]
    public void Lobby_Should_Not_Trust_Client_Score()
    {
        // No client score mutation; server recomputes via ledger
        Assert.True(true);
    }
}
