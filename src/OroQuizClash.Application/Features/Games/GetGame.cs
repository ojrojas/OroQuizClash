using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Shared.Errors;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Games;

public sealed record GetGameQuery(Guid GameId) : IQuery<Result<GameDetailResponse>>;

public sealed record PlayerBriefResponse(Guid UserId, string DisplayName, string Status, int CurrentPoints, DateTimeOffset JoinedAt);
public sealed record GameDetailResponse(
    Guid Id, string Name, string Status, Guid CategoryId, string CategoryName,
    int MinRounds, int MaxRounds,
    int InitialDifficulty, string DifficultyStrategy,
    int TimeLimitPerQuestionSeconds, int PointsPerRound,
    string ScoringSystem, string LossPolicy, string WithdrawalPolicy, string ConsolationPolicy,
    int MinPlayers, int MaxPlayers,
    int PlayerCount, int RoundCount,
    IReadOnlyList<PlayerBriefResponse> Players,
    string Prize,
    string RowVersion,
    DateTimeOffset CreatedAt, DateTimeOffset? ReadyAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt)
{
    // Compatibilidad con GameResponse para deserialización del frontend que espera campos planos
    public int PlayersCurrent => Players.Count;
    public int PlayersMax => MaxPlayers;
}

public sealed class GetGameHandler(
    IRepository<Game, GameId> repository,
    IRepository<Category, CategoryId> categoryRepository) : IQueryHandler<GetGameQuery, Result<GameDetailResponse>>
{
    public async Task<Result<GameDetailResponse>> HandleAsync(GetGameQuery query, CancellationToken ct)
    {
        var spec = new GameByIdSpecification(new GameId(query.GameId));
        var game = await repository.FirstOrDefaultAsync(spec, ct);
        if (game is null) return Result.Failure<GameDetailResponse>(GameErrors.GameNotFound);
        string categoryName = "—";
        try
        {
            var cat = await categoryRepository.GetByIdAsync(game.Configuration.CategoryId, ct);
            if (cat != null) categoryName = cat.Name;
        }
        catch { }
        var players = game.Players.Select(p => new PlayerBriefResponse(
            p.UserId, p.DisplayName ?? $"Player {p.UserId.ToString()[..8]}", p.ParticipationStatus.Name, p.Score.CurrentPoints, p.JoinedAt)).ToList();
        return Result.Success(new GameDetailResponse(
            game.Id.Value, game.Name, game.Status.Name, game.Configuration.CategoryId.Value, categoryName,
            game.Configuration.MinRounds, game.Configuration.MaxRounds,
            game.Configuration.InitialDifficulty, game.Configuration.DifficultyStrategy.Name,
            game.Configuration.TimeLimitPerQuestionSeconds, game.Configuration.PointsPerRound,
            game.Configuration.ScoringSystem.Name, game.Configuration.LossPolicy.Name, game.Configuration.WithdrawalPolicy.Name, game.Configuration.ConsolationPolicy.Name,
            game.Configuration.MinPlayers, game.Configuration.MaxPlayers,
            game.Players.Count, game.Rounds.Count,
            players,
            game.Configuration.RewardRules?.Type ?? "—",
            game.RowVersion != null && game.RowVersion.Length > 0 ? Convert.ToBase64String(game.RowVersion) : string.Empty,
            game.CreatedAt, game.ReadyAt, game.StartedAt, game.FinishedAt));
    }
}

public sealed record GetGamesQuery(string? Status, Guid? CategoryId, Guid? CreatedBy, string? Search, int Page = 1, int PageSize = 20) : IQuery<Result<PaginatedGamesResponse>>;

public sealed record PaginatedGamesResponse(IReadOnlyList<GameResponse> Items, int Total, int Page, int PageSize);

public sealed class GetGamesHandler(IRepository<Game, GameId> repository) : IQueryHandler<GetGamesQuery, Result<PaginatedGamesResponse>>
{
    public async Task<Result<PaginatedGamesResponse>> HandleAsync(GetGamesQuery query, CancellationToken ct)
    {
        var spec = new GameFilterSpecification(query.Status, query.CategoryId, query.CreatedBy, query.Search, query.Page, query.PageSize, true);
        var countSpec = new GameFilterSpecification(query.Status, query.CategoryId, query.CreatedBy, query.Search, 1, 20, false);
        var items = await repository.ListAsync(spec, ct);
        var total = await repository.CountAsync(countSpec, ct);
        var mapped = items.Select(g => new GameResponse(
            g.Id.Value, g.Name, g.Status.Name, g.Configuration.CategoryId.Value,
            g.Configuration.MinRounds, g.Configuration.MaxRounds,
            g.Players.Count, g.Rounds.Count,
            g.RowVersion != null && g.RowVersion.Length > 0 ? Convert.ToBase64String(g.RowVersion) : string.Empty,
            g.CreatedAt, g.ReadyAt, g.StartedAt, g.FinishedAt)).ToList();
        return Result.Success(new PaginatedGamesResponse(mapped, total, query.Page, query.PageSize));
    }
}

public sealed class GetGameEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/games/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetGameQuery(id), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();

        app.MapGet("/api/games", async (string? status, Guid? categoryId, Guid? createdBy, string? search, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetGamesQuery(status, categoryId, createdBy, search, page ?? 1, pageSize ?? 20), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
