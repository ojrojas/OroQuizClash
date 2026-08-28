using BuildingBlocks.Kernel.Domain.Repositories;

using Microsoft.AspNetCore.SignalR;

using OroQuizClash.Application.Features.Games;
using OroQuizClash.Domain.Games;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Api.Hubs;

/// <summary>
/// Broadcast-only hub for multiplayer game notifications (SPEC-011 FR-014).
/// Never a source of truth: clients re-query REST endpoints for authoritative
/// state. No game-command methods — all mutations go through REST.
/// </summary>
public sealed class GameHub(IRepository<Game, GameId> gameRepository) : Hub
{
    public static string GroupName(Guid gameId) => $"game-{gameId}";

    public async Task JoinGameGroup(Guid gameId)
    {
        var user = Context.User ?? throw new HubException("Not authenticated.");
        var sub = GameClaims.GetSub(user);

        var game = await gameRepository.FirstOrDefaultAsync(
            new GameByIdSpecification(gameId), Context.ConnectionAborted);
        if (game is null)
            throw new HubException("Game not found.");

        var isPlayer = game.Players.Any(p => p.UserId == sub);
        if (!isPlayer && !GameClaims.IsOrganizer(user))
            throw new HubException("Only players of this game or organizers may subscribe.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId), Context.ConnectionAborted);
    }
}
