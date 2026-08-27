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
    public DateTimeOffset? ReadyAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    private readonly List<GamePlayer> _players = [];
    public IReadOnlyList<GamePlayer> Players => _players.AsReadOnly();

    private readonly List<GameRound> _rounds = [];
    public IReadOnlyList<GameRound> Rounds => _rounds.AsReadOnly();

    public GameRound? CurrentRound => _rounds.FirstOrDefault(r => r.Status == GameStatus.RoundInProgress);

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

    // DRAFT -> READY gate: config valid + category published + >=5 valid questions
    public Result MarkReady(Func<Guid, bool> isCategoryPublished, Func<Guid, int> countValidQuestions)
    {
        if (Status != GameStatus.Draft)
            return Result.Failure(GameErrors.InvalidGameStateDetail($"MarkReady only from DRAFT, current is {Status.Name}"));

        if (!GameStatus.IsValidTransition(Status, GameStatus.Ready))
            return Result.Failure(GameErrors.InvalidGameState);

        // Re-validate config (in case it changed or category became invalid)
        var cfg = Configuration;
        var nameRule = new GameNameNotEmptyRule(cfg.Name);
        if (nameRule.IsBroken()) return Result.Failure<Game>(GameErrors.InvalidName);
        var minRule = new MinRoundsAtLeastFiveRule(cfg.MinRounds);
        if (minRule.IsBroken()) return Result.Failure<Game>(GameErrors.MinRoundsTooLow);

        if (!isCategoryPublished(cfg.CategoryId.Value))
            return Result.Failure(GameErrors.CategoryNotReady);

        var validCount = countValidQuestions(cfg.CategoryId.Value);
        if (validCount < 5)
            return Result.Failure(GameErrors.CategoryNotReady);

        Status = GameStatus.Ready;
        ReadyAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new GameReadyDomainEvent(Id.Value));
        return Result.Success();
    }

    // READY -> WAITING_FOR_PLAYERS
    public Result OpenLobby()
    {
        if (Status != GameStatus.Ready)
            return Result.Failure(GameErrors.InvalidGameStateDetail($"OpenLobby only from READY, current is {Status.Name}"));

        if (!GameStatus.IsValidTransition(Status, GameStatus.WaitingForPlayers))
            return Result.Failure(GameErrors.InvalidGameState);

        Status = GameStatus.WaitingForPlayers;
        return Result.Success();
    }

    // WAITING_FOR_PLAYERS -> add player
    public Result JoinPlayer(Guid userId, string? displayName = null)
    {
        if (Status != GameStatus.WaitingForPlayers)
            return Result.Failure(GameErrors.InvalidGameStateDetail($"JoinPlayer only in WAITING_FOR_PLAYERS, current is {Status.Name}"));

        if (_players.Any(p => p.UserId == userId))
            return Result.Failure(GameErrors.PlayerAlreadyJoined);

        if (_players.Count >= Configuration.MaxPlayers)
            return Result.Failure(GameErrors.GameFull);

        var player = new GamePlayer(GamePlayerId.New(), Id, userId, displayName);
        _players.Add(player);
        RaiseDomainEvent(new PlayerJoinedDomainEvent(Id.Value, userId));
        return Result.Success();
    }

    // WAITING_FOR_PLAYERS -> IN_PROGRESS
    public Result Start()
    {
        if (Status != GameStatus.WaitingForPlayers)
            return Result.Failure(GameErrors.InvalidGameStateDetail($"Start only from WAITING_FOR_PLAYERS, current is {Status.Name}"));

        if (_players.Count < Configuration.MinPlayers)
            return Result.Failure(GameErrors.NotEnoughPlayers);

        if (_players.Count > Configuration.MaxPlayers)
            return Result.Failure(GameErrors.GameFull);

        if (!GameStatus.IsValidTransition(Status, GameStatus.InProgress))
            return Result.Failure(GameErrors.InvalidGameState);

        Status = GameStatus.InProgress;
        StartedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new GameStartedDomainEvent(Id.Value));
        return Result.Success();
    }

    // IN_PROGRESS or ROUND_COMPLETED -> ROUND_IN_PROGRESS (creates GameRound, assigns QuestionId)
    public Result<GameRound> StartRound(Guid questionId)
    {
        if (Status != GameStatus.InProgress && Status != GameStatus.RoundCompleted)
            return Result.Failure<GameRound>(GameErrors.InvalidGameStateDetail($"StartRound only from IN_PROGRESS or ROUND_COMPLETED, current is {Status.Name}"));

        if (CurrentRound != null)
            return Result.Failure<GameRound>(GameErrors.RoundAlreadyInProgress);

        if (_rounds.Count >= Configuration.MaxRounds)
            return Result.Failure<GameRound>(GameErrors.InvalidGameStateDetail("MaxRounds reached."));

        if (questionId == Guid.Empty)
            return Result.Failure<GameRound>(GameErrors.NoAvailableQuestion);

        // Ensure question not used previously
        var qId = new Questions.QuestionId(questionId);
        if (_rounds.Any(r => r.QuestionId == qId))
            return Result.Failure<GameRound>(GameErrors.InvalidGameStateDetail("Question already used in this game."));

        var roundId = GameRoundId.New();
        var roundNumber = _rounds.Count + 1;
        var round = new GameRound(roundId, Id, roundNumber, qId);
        _rounds.Add(round);
        Status = GameStatus.RoundInProgress;
        RaiseDomainEvent(new RoundStartedDomainEvent(Id.Value, roundId.Value, roundNumber, questionId));
        return Result.Success(round);
    }

    // ROUND_IN_PROGRESS -> ROUND_COMPLETED
    public Result CompleteRound(Guid roundId)
    {
        if (Status != GameStatus.RoundInProgress)
            return Result.Failure(GameErrors.InvalidGameStateDetail($"CompleteRound only from ROUND_IN_PROGRESS, current is {Status.Name}"));

        var round = _rounds.FirstOrDefault(r => r.Id.Value == roundId);
        if (round == null)
            return Result.Failure(GameErrors.InvalidGameStateDetail("Round not found."));

        if (round.Status != GameStatus.RoundInProgress)
            return Result.Failure(GameErrors.InvalidGameStateDetail("Round not in progress."));

        round.Complete();
        Status = GameStatus.RoundCompleted;
        RaiseDomainEvent(new RoundCompletedDomainEvent(Id.Value, roundId));
        return Result.Success();
    }

    // Finish from IN_PROGRESS, ROUND_COMPLETED, or ROUND_IN_PROGRESS (per policy) -> FINISHED
    public Result Finish()
    {
        if (Status != GameStatus.InProgress && Status != GameStatus.RoundCompleted && Status != GameStatus.RoundInProgress)
            return Result.Failure(GameErrors.InvalidGameStateDetail($"Finish only from IN_PROGRESS/ROUND_COMPLETED/ROUND_IN_PROGRESS, current is {Status.Name}"));

        if (!GameStatus.IsValidTransition(Status, GameStatus.Finished))
            return Result.Failure(GameErrors.InvalidGameState);

        Status = GameStatus.Finished;
        FinishedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new GameFinishedDomainEvent(Id.Value));
        return Result.Success();
    }

    // Cancel from non-terminal -> CANCELLED
    public Result Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3 || reason.Trim().Length > 500)
            return Result.Failure(GameErrors.InvalidReason);

        if (Status.IsTerminal)
            return Result.Failure(GameErrors.InvalidGameStateDetail($"Cannot cancel terminal game in {Status.Name}"));

        if (!GameStatus.IsValidTransition(Status, GameStatus.Cancelled))
            return Result.Failure(GameErrors.InvalidGameState);

        Status = GameStatus.Cancelled;
        FinishedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new GameCancelledDomainEvent(Id.Value, reason.Trim()));
        return Result.Success();
    }

    // Force finish from IN_PROGRESS/ROUND_* -> FORCED_FINISHED
    public Result ForceFinish(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3 || reason.Trim().Length > 500)
            return Result.Failure(GameErrors.InvalidReason);

        if (Status != GameStatus.InProgress && Status != GameStatus.RoundInProgress && Status != GameStatus.RoundCompleted)
            return Result.Failure(GameErrors.InvalidGameStateDetail($"ForceFinish only from IN_PROGRESS/ROUND_* states, current is {Status.Name}"));

        if (!GameStatus.IsValidTransition(Status, GameStatus.ForcedFinished))
            return Result.Failure(GameErrors.InvalidGameState);

        Status = GameStatus.ForcedFinished;
        FinishedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new GameForcedFinishedDomainEvent(Id.Value, reason.Trim()));
        return Result.Success();
    }

    public bool CanSubmitAnswer()
    {
        return Status == GameStatus.RoundInProgress && CurrentRound != null;
    }

    public Result UpdateConfiguration(GameConfiguration newConfig)
    {
        if (Status.IsStarted)
            return Result.Failure(GameErrors.ConfigurationImmutable);

        var result = Create(newConfig, CreatedBy);
        if (result.IsFailure) return Result.Failure(result.Error);

        Configuration = newConfig;
        Name = newConfig.Name;
        return Result.Success();
    }
}
