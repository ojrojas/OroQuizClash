using Microsoft.AspNetCore.SignalR;

using NSubstitute;

using OroQuizClash.Api.Hubs;
using OroQuizClash.Application.Features.Games.Notifications;

namespace OroQuizClash.Api.Tests.Hubs;

public sealed class GameHubContractTests
{
    private static (SignalRGameNotificationsBroadcaster broadcaster, IHubContext<GameHub> hubContext, IClientProxy clientProxy) CreateBroadcaster()
    {
        var hubContext = Substitute.For<IHubContext<GameHub>>();
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        var groupManager = Substitute.For<IGroupManager>();
        hubContext.Clients.Returns(clients);
        clients.Group(Arg.Any<string>()).Returns(clientProxy);
        hubContext.Groups.Returns(groupManager);
        var broadcaster = new SignalRGameNotificationsBroadcaster(hubContext);
        return (broadcaster, hubContext, clientProxy);
    }

    [Fact]
    public async Task RoundStarted_SendsCorrectEventName()
    {
        var (broadcaster, _, clientProxy) = CreateBroadcaster();
        var gameId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        await broadcaster.RoundStartedAsync(gameId, roundId, 1);

        await clientProxy.Received(1).SendCoreAsync("RoundStarted", Arg.Is<object[]>(args => args.Length == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuestionPresented_SendsCorrectEventNameAndFiltersIsCorrect()
    {
        var (broadcaster, _, clientProxy) = CreateBroadcaster();
        var gameId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var payload = new QuestionPresentedPayload(Guid.NewGuid(), "Q?", new[] { new QuestionOptionPayload(Guid.NewGuid(), "A"), new QuestionOptionPayload(Guid.NewGuid(), "B") });

        await broadcaster.QuestionPresentedAsync(gameId, roundId, 1, payload);

        await clientProxy.Received(1).SendCoreAsync("QuestionPresented", Arg.Any<object[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RoundCompleted_SendsCorrectEventName()
    {
        var (broadcaster, _, clientProxy) = CreateBroadcaster();
        var gameId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        await broadcaster.RoundCompletedAsync(gameId, roundId, 2);

        await clientProxy.Received(1).SendCoreAsync("RoundCompleted", Arg.Is<object[]>(args => args.Length == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GameStarted_SendsCorrectEventName()
    {
        var (broadcaster, _, clientProxy) = CreateBroadcaster();
        var gameId = Guid.NewGuid();

        await broadcaster.GameStartedAsync(gameId);

        await clientProxy.Received(1).SendCoreAsync("GameStarted", Arg.Any<object[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayerAnswered_SendsCorrectEventNameWithoutForbiddenFields()
    {
        var (broadcaster, _, clientProxy) = CreateBroadcaster();
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        await broadcaster.PlayerAnsweredAsync(gameId, playerId, roundId, DateTimeOffset.UtcNow);

        await clientProxy.Received(1).SendCoreAsync("PlayerAnswered", Arg.Any<object[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GameFinished_SendsCorrectEventName()
    {
        var (broadcaster, _, clientProxy) = CreateBroadcaster();
        var gameId = Guid.NewGuid();

        await broadcaster.GameFinishedAsync(gameId, "FINISHED", Array.Empty<OroQuizClash.Application.Features.Games.LeaderboardEntryResponse>());

        await clientProxy.Received(1).SendCoreAsync("GameFinished", Arg.Any<object[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GroupName_IsGamePrefixed()
    {
        var gameId = Guid.NewGuid();
        Assert.Equal($"game-{gameId}", GameHub.GroupName(gameId));
    }
}
