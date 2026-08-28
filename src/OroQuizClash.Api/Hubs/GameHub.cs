using BuildingBlocks.Kernel.Domain.Repositories;

using Microsoft.AspNetCore.SignalR;

using OroQuizClash.Application.Features.Games;
using OroQuizClash.Domain.Games;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Api.Hubs;

/// <summary>
/// Broadcast-only hub for realtime game notifications (SPEC-011 FR-014, SPEC-012 FR-001).
/// Never a source of truth: clients re-query REST endpoints for authoritative
/// state. No game-command methods — all mutations go through REST.
///
/// Catalog (9 events, all server→client to group game-{gameId}):
/// - GameStarted, PlayerJoined, RoundStarted, QuestionPresented, PlayerAnswered,
///   ScoreUpdated, LeaderboardUpdated, RoundCompleted, GameFinished
///
/// Connection: RequireAuthorization (JWT OroIdentityServer).
/// Subscription: client invokes JoinGameGroup(gameId) — validates sub ∈ game.Players
/// or IsOrganizer; joins group game-{gameId}. Multiple connections per player
/// are supported. Events are best-effort, not replayed — clients re-query REST
/// on reconnect (FR-015/FR-019).
///
/// FR-012 note: withdrawn/eliminated players remain in group but
/// RoundStarted/QuestionPresented/PlayerAnswered/RoundCompleted payloads
/// are logically ignored by client after WITHDRAWN/ELIMINATED;
/// server-side sub-group game-{gameId}-active is future optimization (R8).
/// TODO: implement game-{gameId}-active sub-group filtering for withdrawn players if required.
///
/// Example JS:
///   const c = new signalR.HubConnectionBuilder().withUrl("/hubs/game", { accessTokenFactory: () => token }).withAutomaticReconnect().build();
///   c.on("GameStarted", p => {});
///   c.on("PlayerJoined", p => {});
///   c.on("RoundStarted", p => {});
///   c.on("QuestionPresented", p => {}); // p.question.answerOptions has no isCorrect
///   c.on("PlayerAnswered", p => {});    // no answerOptionId/correct/points
///   c.on("ScoreUpdated", p => {});
///   c.on("LeaderboardUpdated", p => {});
///   c.on("RoundCompleted", p => {});
///   c.on("GameFinished", p => {});
///   await c.start(); await c.invoke("JoinGameGroup", gameId);
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
