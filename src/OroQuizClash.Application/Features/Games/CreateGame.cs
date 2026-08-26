using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Application.Features.Games;

public sealed record CreateGameCommand(
    string Name,
    Guid CategoryId,
    int MinRounds,
    int MaxRounds,
    int InitialDifficulty,
    string DifficultyStrategy,
    int TimeLimitPerQuestionSeconds,
    string ScoringSystem,
    string LossPolicy,
    string WithdrawalPolicy,
    string ConsolationPolicy,
    string RewardType,
    int RewardThreshold,
    int MinPlayers,
    int MaxPlayers) : ICommand<Result<CreateGameResponse>>;

public sealed record CreateGameResponse(Guid GameId, string Status, CreateGameCommand Configuration);

public sealed class CreateGameValidator : IValidator<CreateGameCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(CreateGameCommand request, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 3 || request.Name.Trim().Length > 100)
            failures.Add(new ValidationFailure(nameof(request.Name), "Name must be 3-100 characters."));
        if (request.CategoryId == Guid.Empty)
            failures.Add(new ValidationFailure(nameof(request.CategoryId), "CategoryId is required."));
        if (request.MinRounds < 5)
            failures.Add(new ValidationFailure(nameof(request.MinRounds), "MinRounds must be >= 5."));
        if (request.MaxRounds < 5)
            failures.Add(new ValidationFailure(nameof(request.MaxRounds), "MaxRounds must be >= 5."));
        if (request.MinRounds > request.MaxRounds)
            failures.Add(new ValidationFailure(nameof(request.MinRounds), "MinRounds must be <= MaxRounds."));
        if (request.MinPlayers < 1 || request.MaxPlayers < 1 || request.MinPlayers > request.MaxPlayers)
            failures.Add(new ValidationFailure(nameof(request.MinPlayers), "Players range invalid: min >=1 and min <= max."));
        if (request.TimeLimitPerQuestionSeconds < 5 || request.TimeLimitPerQuestionSeconds > 300)
            failures.Add(new ValidationFailure(nameof(request.TimeLimitPerQuestionSeconds), "TimeLimit must be 5-300 seconds."));
        if (string.IsNullOrWhiteSpace(request.DifficultyStrategy))
            failures.Add(new ValidationFailure(nameof(request.DifficultyStrategy), "DifficultyStrategy is required."));
        if (string.IsNullOrWhiteSpace(request.ScoringSystem))
            failures.Add(new ValidationFailure(nameof(request.ScoringSystem), "ScoringSystem is required."));
        if (string.IsNullOrWhiteSpace(request.LossPolicy))
            failures.Add(new ValidationFailure(nameof(request.LossPolicy), "LossPolicy is required."));
        if (string.IsNullOrWhiteSpace(request.WithdrawalPolicy))
            failures.Add(new ValidationFailure(nameof(request.WithdrawalPolicy), "WithdrawalPolicy is required."));
        if (string.IsNullOrWhiteSpace(request.ConsolationPolicy))
            failures.Add(new ValidationFailure(nameof(request.ConsolationPolicy), "ConsolationPolicy is required."));
        if (string.IsNullOrWhiteSpace(request.RewardType))
            failures.Add(new ValidationFailure(nameof(request.RewardType), "RewardType is required."));

        // Enum validation
        try { DifficultyProgressionStrategy.FromName(request.DifficultyStrategy); } catch { failures.Add(new ValidationFailure(nameof(request.DifficultyStrategy), $"Unknown DifficultyStrategy '{request.DifficultyStrategy}'.")); }
        try { ScoringSystem.FromName(request.ScoringSystem); } catch { failures.Add(new ValidationFailure(nameof(request.ScoringSystem), $"Unknown ScoringSystem '{request.ScoringSystem}'.")); }
        try { LossPolicy.FromName(request.LossPolicy); } catch { failures.Add(new ValidationFailure(nameof(request.LossPolicy), $"Unknown LossPolicy '{request.LossPolicy}'.")); }
        try { WithdrawalPolicy.FromName(request.WithdrawalPolicy); } catch { failures.Add(new ValidationFailure(nameof(request.WithdrawalPolicy), $"Unknown WithdrawalPolicy '{request.WithdrawalPolicy}'.")); }
        try { ConsolationPolicy.FromName(request.ConsolationPolicy); } catch { failures.Add(new ValidationFailure(nameof(request.ConsolationPolicy), $"Unknown ConsolationPolicy '{request.ConsolationPolicy}'.")); }
        if (request.InitialDifficulty < 1 || request.InitialDifficulty > 5)
            failures.Add(new ValidationFailure(nameof(request.InitialDifficulty), "InitialDifficulty must be 1-5."));

        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class CreateGameHandler(
    IRepository<Game, GameId> games,
    ICategoryValidator categoryValidator,
    IUnitOfWork unitOfWork,
    IRepository<Category, CategoryId>? categoryRepository = null,
    IQuestionCounter? questionCounter = null)
    : ICommandHandler<CreateGameCommand, Result<CreateGameResponse>>
{
    public async Task<Result<CreateGameResponse>> HandleAsync(CreateGameCommand command, CancellationToken ct)
    {
        var categoryId = new CategoryId(command.CategoryId);
        var exists = await categoryValidator.ExistsAsync(categoryId, ct);
        if (!exists) return Result.Failure<CreateGameResponse>(GameErrors.CategoryNotFound);

        // Integration guard 002→001: ensure Category Status == ACTIVE and >=5 valid questions.
        // If IRepository<Category> is available, use it for precise Status check + IQuestionCounter count.
        if (categoryRepository is not null)
        {
            var category = await categoryRepository.GetByIdAsync(categoryId, ct);
            if (category is null) return Result.Failure<CreateGameResponse>(GameErrors.CategoryNotFound);
            if (category.Status != CategoryStatus.Active)
                return Result.Failure<CreateGameResponse>(GameErrors.CategoryNotReady);
            if (questionCounter is not null)
            {
                var validCount = await questionCounter.CountValidAsync(categoryId, ct);
                if (validCount < 5) return Result.Failure<CreateGameResponse>(GameErrors.CategoryNotReady);
            }
            else
            {
                var published = await categoryValidator.IsPublishedAsync(categoryId, ct);
                if (!published) return Result.Failure<CreateGameResponse>(GameErrors.CategoryNotReady);
            }
        }
        else
        {
            var published = await categoryValidator.IsPublishedAsync(categoryId, ct);
            if (!published) return Result.Failure<CreateGameResponse>(GameErrors.CategoryNotReady);
        }

        var difficultyStrategy = DifficultyProgressionStrategy.FromName(command.DifficultyStrategy);
        var scoring = ScoringSystem.FromName(command.ScoringSystem);
        var loss = LossPolicy.FromName(command.LossPolicy);
        var withdrawal = WithdrawalPolicy.FromName(command.WithdrawalPolicy);
        var consolation = ConsolationPolicy.FromName(command.ConsolationPolicy);

        var config = new GameConfiguration(
            command.Name.Trim(),
            categoryId,
            command.MinRounds,
            command.MaxRounds,
            command.InitialDifficulty,
            difficultyStrategy,
            command.TimeLimitPerQuestionSeconds,
            scoring,
            loss,
            withdrawal,
            consolation,
            new RewardRules(command.RewardType, command.RewardThreshold),
            command.MinPlayers,
            command.MaxPlayers);

        var result = Game.Create(config, Guid.NewGuid());
        if (result.IsFailure) return Result.Failure<CreateGameResponse>(result.Error);

        var game = result.Value;
        await games.AddAsync(game, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var response = new CreateGameResponse(game.Id.Value, game.Status.Name, command);
        return Result.Success(response);
    }
}

public sealed class CreateGameEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/games", async (CreateGameCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(command, ct);
            return result.ToCreatedResult(r => $"/api/games/{r.GameId}");
        }).RequireAuthorization("AdminOrGameManager");
    }
}