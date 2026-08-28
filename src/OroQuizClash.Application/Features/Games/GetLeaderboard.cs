using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Shared.Errors;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Games;

public sealed record GetLeaderboardQuery(Guid GameId) : IQuery<Result<LeaderboardResponse>>;

public sealed record LeaderboardResponse(
    Guid GameId,
    IReadOnlyList<LeaderboardEntryResponse> Players);

public sealed record LeaderboardEntryResponse(
    Guid PlayerId,
    string? DisplayName,
    int Rank,
    int Points,
    int CorrectAnswers,
    int? CurrentLevel,
    string Status,
    int SecuredPoints);

public sealed class GetLeaderboardHandler(IRepository<Game, GameId> repository) : IQueryHandler<GetLeaderboardQuery, Result<LeaderboardResponse>>
{
    public async Task<Result<LeaderboardResponse>> HandleAsync(GetLeaderboardQuery query, CancellationToken ct)
    {
        var spec = new GameByIdWithAnswersSpecification(new GameId(query.GameId));
        var game = await repository.FirstOrDefaultAsync(spec, ct);
        if (game is null) return Result.Failure<LeaderboardResponse>(GameErrors.GameNotFound);

        return Result.Success(new LeaderboardResponse(query.GameId, LeaderboardBuilder.Build(game)));
    }
}

public sealed class GetLeaderboardEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/games/{id:guid}/leaderboard", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetLeaderboardQuery(id), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
