using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.Results;

using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Events;
using OroQuizClash.Domain.Games.Rules;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Domain.Games;

public sealed class Game : AggregateRoot<GameId>
{
    public string Name { get; private set; } = string.Empty;
    public GameConfiguration Configuration { get; private set; } = null!;
    public GameStatus Status { get; private set; } = GameStatus.Draft;
    public byte[] RowVersion { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    private Game() { }

    private Game(GameId id, string name, GameConfiguration config, Guid createdBy)
        : base(id)
    {
        Name = name;
        Configuration = config;
        Status = GameStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = createdBy;
    }

    public static Result<Game> Create(GameConfiguration config, Guid createdBy)
    {
        var nameRule = new GameNameNotEmptyRule(config.Name);
        if (nameRule.IsBroken()) return Result.Failure<Game>(GameErrors.InvalidName);

        var minRule = new MinRoundsAtLeastFiveRule(config.MinRounds);
        if (minRule.IsBroken()) return Result.Failure<Game>(GameErrors.MinRoundsTooLow);

        var roundsRule = new RoundsRangeCoherenceRule(config.MinRounds, config.MaxRounds);
        if (roundsRule.IsBroken()) return Result.Failure<Game>(GameErrors.InvalidRange);

        var playersRule = new PlayersRangeCoherenceRule(config.MinPlayers, config.MaxPlayers);
        if (playersRule.IsBroken()) return Result.Failure<Game>(GameErrors.InvalidRange);

        var timePositive = new TimeLimitPositiveRule(config.TimeLimitPerQuestionSeconds);
        if (timePositive.IsBroken()) return Result.Failure<Game>(GameErrors.InvalidTimeLimit);

        var timeRange = new TimeLimitRangeRule(config.TimeLimitPerQuestionSeconds);
        if (timeRange.IsBroken()) return Result.Failure<Game>(GameErrors.InvalidTimeLimit);

        var strategyRule = new DifficultyStrategyRequiredRule(config.DifficultyStrategy);
        if (strategyRule.IsBroken()) return Result.Failure<Game>(GameErrors.DifficultyStrategyRequired);

        var policiesRule = new PoliciesRequiredRule(config.LossPolicy, config.WithdrawalPolicy);
        if (policiesRule.IsBroken()) return Result.Failure<Game>(GameErrors.PoliciesRequired);

        if (config.InitialDifficulty < 1 || config.InitialDifficulty > 5)
            return Result.Failure<Game>(GameErrors.IncompatibleDifficulty);

        if (config.CategoryId.Value == Guid.Empty)
            return Result.Failure<Game>(GameErrors.CategoryNotFound);

        if (config.RewardRules is null || string.IsNullOrWhiteSpace(config.RewardRules.Type))
            return Result.Failure<Game>(GameErrors.InvalidGameConfiguration("RewardRules is required."));

        if (config.ScoringSystem is null)
            return Result.Failure<Game>(GameErrors.InvalidGameConfiguration("ScoringSystem is required."));

        var game = new Game(GameId.New(), config.Name, config, createdBy);
        game.RaiseDomainEvent(new GameCreatedDomainEvent(game.Id.Value));
        return Result.Success<Game>(game);
    }

    public Result Start()
    {
        if (Status.IsStarted)
            return Result.Failure(GameErrors.ConfigurationImmutable);

        if (Status != GameStatus.Draft && Status != GameStatus.Ready)
            return Result.Failure(GameErrors.InvalidGameState);

        Status = GameStatus.WaitingForPlayers;
        RaiseDomainEvent(new GameStartedDomainEvent(Id.Value));
        return Result.Success();
    }

    public Result UpdateConfiguration(GameConfiguration newConfig)
    {
        if (Status.IsStarted)
            return Result.Failure(GameErrors.ConfigurationImmutable);

        // Re-validate
        var result = Create(newConfig, CreatedBy);
        if (result.IsFailure) return Result.Failure(result.Error);

        Configuration = newConfig;
        Name = newConfig.Name;
        return Result.Success();
    }
}