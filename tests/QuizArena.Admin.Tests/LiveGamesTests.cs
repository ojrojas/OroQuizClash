using QuizArena.Admin.Client.Models;
using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Tests;

/// <summary>T052: Live subscription event filtering + connection state.</summary>
public sealed class LiveGamesTests
{
    [Theory]
    [InlineData("QuestionPresented", false)]
    [InlineData("PlayerAnswered", false)]
    [InlineData("ScoreUpdated", false)]
    [InlineData("GameStarted", true)]
    [InlineData("PlayerJoined", true)]
    [InlineData("RoundStarted", true)]
    [InlineData("RoundCompleted", true)]
    [InlineData("GameFinished", true)]
    [InlineData("LeaderboardUpdated", true)]
    public void IsAdminEvent_FiltersCorrectly(string eventName, bool isAdmin)
    {
        Assert.Equal(isAdmin, LiveGameSubscription.IsAdminEvent(eventName));
        Assert.Equal(!isAdmin, LiveGameSubscription.IgnoredPrivateEvents.Contains(eventName));
    }

    [Fact]
    public void AdminEvents_ContainsSixEntries() => Assert.Equal(6, LiveGameSubscription.AdminEvents.Length);

    [Fact]
    public void IgnoredPrivateEvents_ContainsThreeEntries() => Assert.Equal(3, LiveGameSubscription.IgnoredPrivateEvents.Length);

    [Fact]
    public void ResyncRequested_IsRaisedOnReconnection()
    {
        // Simulate via SignalR subscription's Reconnected handler: it calls ResyncRequested.
        // Here we verify the abstract base's event wiring works.
        var sub = new FakeSubscription();
        var raised = false;
        sub.ResyncRequested += () => { raised = true; return Task.CompletedTask; };
        sub.RaiseResync();
        Assert.True(raised);
    }

    private sealed class FakeSubscription : LiveGameSubscription
    {
        public override Guid GameId => Guid.NewGuid();
        public override LiveConnectionView ConnectionState => LiveConnectionView.Connected;
        public void RaiseResync() => RaiseResyncRequestedAsync().GetAwaiter().GetResult();
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
