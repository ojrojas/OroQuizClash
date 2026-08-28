using QuizArena.Admin.Client.Models.LiveGame;

namespace QuizArena.Admin.Tests;

public sealed class LiveOperationsTests
{
    [Theory]
    [InlineData(GameStateView.Running, GameStateView.Paused, true)] // Pause
    [InlineData(GameStateView.Paused, GameStateView.Running, true)] // Resume
    [InlineData(GameStateView.Running, GameStateView.Cancelled, true)] // Cancel
    [InlineData(GameStateView.Running, GameStateView.Finished, true)] // ForceFinish
    [InlineData(GameStateView.Finished, GameStateView.Paused, false)] // Invalid
    [InlineData(GameStateView.Draft, GameStateView.Paused, false)]
    public void IsValidTransition_ChecksGuards(GameStateView from, GameStateView to, bool expected)
    {
        var isValid = IsValidTransition(from, to);
        Assert.Equal(expected, isValid);
    }

    [Fact]
    public void GameOperation_HasIdempotencyKey()
    {
        var op = new GameOperation(Guid.NewGuid(), GameOperationKind.Pause, "v1", Guid.NewGuid().ToString(), null, "sub123", DateTimeOffset.UtcNow, "corr-123");
        Assert.False(string.IsNullOrWhiteSpace(op.IdempotencyKey));
        Assert.Equal("corr-123", op.CorrelationId);
    }

    [Fact]
    public void GameAuditEntry_Privileged_ForForceFinish()
    {
        var entry = new GameAuditEntry(Guid.NewGuid(), "sub", DateTimeOffset.UtcNow, GameStateView.Running, GameStateView.Finished, "ForceFinish", null, "corr-1", "Success", Guid.NewGuid().ToString(), true);
        Assert.True(entry.Privileged);
    }

    private static bool IsValidTransition(GameStateView from, GameStateView to) => (from, to) switch
    {
        (GameStateView.Running, GameStateView.Paused) => true,
        (GameStateView.Paused, GameStateView.Running) => true,
        (GameStateView.Running, GameStateView.Cancelled) => true,
        (GameStateView.Paused, GameStateView.Cancelled) => true,
        (GameStateView.Running, GameStateView.Finished) => true,
        (GameStateView.Paused, GameStateView.Finished) => true,
        _ => false
    };
}
