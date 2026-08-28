using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Specifications;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

using NSubstitute;

using OroQuizClash.Api.Hubs;
using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;

using System.Security.Claims;

namespace OroQuizClash.Api.Tests.Hubs;

public sealed class GameHubIsolationTests
{
    private static Game CreateGame(out Guid playerId)
    {
        var config = new GameConfiguration(
            "Isolation Game",
            new CategoryId(Guid.NewGuid()),
            5, 10, 1,
            DifficultyProgressionStrategy.Linear, 30,
            ScoringSystem.Standard, LossPolicy.LoseCurrentRound,
            WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None,
            new RewardRules("Points", 1000),
            2, 10, 100);
        var game = Game.Create(config, Guid.NewGuid()).Value;
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        playerId = Guid.NewGuid();
        game.JoinPlayer(playerId, "Alice");
        game.JoinPlayer(Guid.NewGuid(), "Bob");
        game.Start();
        return game;
    }

    private static IRepository<Game, GameId> Repo(Game? game)
    {
        var repo = Substitute.For<IRepository<Game, GameId>>();
        repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Game>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(game));
        return repo;
    }

    private static HubCallerContext ContextWithUser(Guid sub, string role = "PLAYER")
    {
        var claims = new List<Claim> { new("sub", sub.ToString()), new("role", role), new("name", "Test") };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var context = Substitute.For<HubCallerContext>();
        context.User.Returns(principal);
        context.ConnectionId.Returns(Guid.NewGuid().ToString());
        context.ConnectionAborted.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task JoinGameGroup_NonMember_ThrowsHubException()
    {
        var game = CreateGame(out _);
        var outsider = Guid.NewGuid();
        var hub = new GameHub(Repo(game))
        {
            Context = ContextWithUser(outsider)
        };
        hub.Groups = Substitute.For<IGroupManager>();
        hub.Groups.AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<HubException>(() => hub.JoinGameGroup(game.Id.Value));
    }

    [Fact]
    public async Task JoinGameGroup_Organizer_CanSubscribeWithoutBeingPlayer()
    {
        var game = CreateGame(out _);
        var organizer = Guid.NewGuid();
        var hub = new GameHub(Repo(game))
        {
            Context = ContextWithUser(organizer, "ADMIN")
        };
        var groups = Substitute.For<IGroupManager>();
        hub.Groups = groups;
        groups.AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await hub.JoinGameGroup(game.Id.Value);

        await groups.Received(1).AddToGroupAsync(Arg.Any<string>(), $"game-{game.Id.Value}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGameGroup_Player_CanSubscribe()
    {
        var game = CreateGame(out var playerId);
        var hub = new GameHub(Repo(game))
        {
            Context = ContextWithUser(playerId)
        };
        var groups = Substitute.For<IGroupManager>();
        hub.Groups = groups;
        groups.AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await hub.JoinGameGroup(game.Id.Value);

        await groups.Received(1).AddToGroupAsync(Arg.Any<string>(), $"game-{game.Id.Value}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGameGroup_GameNotFound_ThrowsHubException()
    {
        var hub = new GameHub(Repo(null))
        {
            Context = ContextWithUser(Guid.NewGuid())
        };
        hub.Groups = Substitute.For<IGroupManager>();

        await Assert.ThrowsAsync<HubException>(() => hub.JoinGameGroup(Guid.NewGuid()));
    }
}
