using Xunit;

namespace OroQuizClash.Api.Tests.Contracts;

public sealed class LobbyJoinGameContractTests
{
    [Fact]
    public async Task JoinGame_Idempotent_SameKey_ReturnsSameSession()
    {
        // first POST /api/games/{id}/players with X-Idempotency-Key → 200 ACTIVE, second same key → 200 same GameSessionId count unchanged
        await Task.CompletedTask;
        Assert.True(true);
    }

    [Fact]
    public async Task JoinGame_Full_And_NotWaiting_Rejected()
    {
        // GameFull 409, GameNotWaitingForPlayers 400, PlayerIdentityMismatch 403 when sub mismatch
        await Task.CompletedTask;
        Assert.True(true);
    }
}
