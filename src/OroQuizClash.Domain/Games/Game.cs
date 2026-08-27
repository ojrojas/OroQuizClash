using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.Results;

using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Events;
using OroQuizClash.Domain.Games.Rules;
using OroQuizClash.Domain.Games.Strategies;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Questions;
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

    private readonly List<Answer> _answers = [];
    public IReadOnlyList<Answer> Answers => _answers.AsReadOnly();

    private readonly List<PointTransaction> _pointTransactions = [];
    public IReadOnlyList<PointTransaction> PointTransactions => _pointTransactions.AsReadOnly();

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

    // IN_PROGRESS or ROUND_COMPLETED -> ROUND_IN_PROGRESS (creates GameRound, assigns QuestionId, Difficulty, TimeLimit)
    public Result<GameRound> StartRound(Guid questionId, int difficulty, int? timeLimitOverride = null)
    {
        if (Status != GameStatus.InProgress && Status != GameStatus.RoundCompleted)
            return Result.Failure<GameRound>(GameErrors.InvalidGameStateDetail($"StartRound only from IN_PROGRESS or ROUND_COMPLETED, current is {Status.Name}"));

        if (CurrentRound != null)
            return Result.Failure<GameRound>(GameErrors.RoundAlreadyInProgress);

        if (_rounds.Count >= Configuration.MaxRounds)
            return Result.Failure<GameRound>(GameErrors.InvalidGameStateDetail("MaxRounds reached."));

        if (questionId == Guid.Empty)
            return Result.Failure<GameRound>(GameErrors.NoAvailableQuestion);

        // Validate 5 fields invariants
        if (difficulty < 1 || difficulty > 5)
            return Result.Failure<GameRound>(GameErrors.InvalidGameStateDetail($"Difficulty must be 1..5, got {difficulty}"));

        var timeLimit = timeLimitOverride ?? Configuration.TimeLimitPerQuestionSeconds;
        if (timeLimit < 5 || timeLimit > 300)
            return Result.Failure<GameRound>(GameErrors.InvalidGameStateDetail($"TimeLimit must be 5-300, got {timeLimit}"));

        // Ensure question not used previously (PreviousQuestionIds exclusion)
        var qId = new Questions.QuestionId(questionId);
        if (_rounds.Any(r => r.QuestionId == qId))
            return Result.Failure<GameRound>(GameErrors.InvalidGameStateDetail("Question already used in this game."));

        var roundId = GameRoundId.New();
        var roundNumber = _rounds.Count + 1;
        var round = new GameRound(roundId, Id, roundNumber, difficulty, qId, timeLimit);
        _rounds.Add(round);
        Status = GameStatus.RoundInProgress;

        var potential = ComputeRoundPoints(difficulty);
        foreach (var p in _players.Where(p => p.IsActive))
            p.UpdateScore(p.Score.ResetRound().SetPotential(potential));

        RaiseDomainEvent(new RoundStartedDomainEvent(Id.Value, roundId.Value, roundNumber, questionId));
        return Result.Success(round);
    }

    // Overload for backward compatibility (used by existing tests) - computes difficulty via linear progression
    public Result<GameRound> StartRound(Guid questionId) => StartRound(questionId, ComputeLinearDifficulty(), Configuration.TimeLimitPerQuestionSeconds);

    private int ComputeLinearDifficulty()
    {
        var completed = _rounds.Count(r => r.Status == GameStatus.RoundCompleted);
        return Math.Clamp(Configuration.InitialDifficulty + completed, 1, 5);
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

        var previousMaxDifficulty = _rounds
            .Where(r => r.Id.Value != roundId && r.Status == GameStatus.RoundCompleted)
            .Select(r => r.Difficulty)
            .DefaultIfEmpty(0)
            .Max();

        round.Complete();
        Status = GameStatus.RoundCompleted;

        // SPEC-007 US3: Secure points + bonuses for active players
        foreach (var player in _players.Where(p => p.IsActive))
        {
            SecurePoints(player.UserId);

            if (Configuration.ScoringSystem == ScoringSystem.ProgressiveBonus)
                AwardPointsInternal(player, round.RoundNumber, PointTransactionType.RoundBonus, round.Id, null, null, roundScoped: false);

            if (round.Difficulty > previousMaxDifficulty && previousMaxDifficulty > 0)
                AwardPointsInternal(player, Configuration.PointsPerRound, PointTransactionType.LevelBonus, round.Id, null, null, roundScoped: false);
        }

        RaiseDomainEvent(new RoundCompletedDomainEvent(Id.Value, roundId));
        return Result.Success();
    }

    // Finish from IN_PROGRESS, ROUND_COMPLETED, or ROUND_IN_PROGRESS (per policy) -> FINISHED, gate MinRounds
    public Result Finish()
    {
        if (Status != GameStatus.InProgress && Status != GameStatus.RoundCompleted && Status != GameStatus.RoundInProgress)
            return Result.Failure(GameErrors.InvalidGameStateDetail($"Finish only from IN_PROGRESS/ROUND_COMPLETED/ROUND_IN_PROGRESS, current is {Status.Name}"));

        var completed = _rounds.Count(r => r.Status == GameStatus.RoundCompleted);
        if (completed < Configuration.MinRounds)
            return Result.Failure(GameErrors.InvalidGameStateDetail($"Not enough rounds to finish: completed {completed} < MinRounds {Configuration.MinRounds}"));

        if (!GameStatus.IsValidTransition(Status, GameStatus.Finished))
            return Result.Failure(GameErrors.InvalidGameState);

        // SPEC-007 US6+US9: Game bonus + consolation for eligible players
        var completedRounds = _rounds.Count(r => r.Status == GameStatus.RoundCompleted);
        var activePlayers = _players.Where(p => p.IsActive).ToList();
        var maxScore = activePlayers.Select(p => p.Score.CurrentPoints).DefaultIfEmpty(0).Max();

        var consolationEligible = activePlayers
            .Where(p => p.Score.CurrentPoints < maxScore && completedRounds >= Configuration.MinRounds)
            .Select(p => p.Id)
            .ToHashSet();

        foreach (var player in activePlayers)
        {
            AwardPointsInternal(player, Configuration.PointsPerRound, PointTransactionType.GameBonus, null, null, null, roundScoped: false);

            if (Configuration.ConsolationPolicy == ConsolationPolicy.FixedPoints && consolationEligible.Contains(player.Id))
                AwardPointsInternal(player, Configuration.PointsPerRound, PointTransactionType.Consolation, null, null, null, roundScoped: false);
        }

        // SPEC-008 US3: Winner determination — all active players with max final score (ties all win)
        var finalMaxScore = activePlayers.Select(p => p.Score.CurrentPoints).DefaultIfEmpty(0).Max();
        foreach (var player in activePlayers.Where(p => p.Score.CurrentPoints == finalMaxScore))
            player.MarkWinner();

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

    public Result<Answer> SubmitAnswer(
        Guid playerId,
        AnswerOptionId answerOptionId,
        DateTimeOffset serverTimestamp,
        Func<QuestionId, Question?> questionResolver)
    {
        // Step 1: ValidatePlayer
        var player = _players.FirstOrDefault(p => p.UserId == playerId);
        var isPlayerInProgress = player != null;
        var playerRule = new ValidatePlayerRule(isPlayerInProgress);
        if (playerRule.IsBroken() || player is null)
            return Result.Failure<Answer>(GameErrors.PlayerNotInGame);

        // Step 2: ValidateGame
        var gameRule = new ValidateGameRule(Status);
        if (gameRule.IsBroken())
            return Result.Failure<Answer>(GameErrors.GameNotActive);

        // Step 3: ValidateRound
        var currentRound = CurrentRound;
        var roundRule = new ValidateRoundRule(currentRound);
        if (roundRule.IsBroken())
            return Result.Failure<Answer>(GameErrors.QuestionNotActive);

        // Step 4: ValidateQuestion — AnswerOptionId must belong to the Question of this round
        var question = questionResolver(currentRound!.QuestionId);
        if (question is null)
            return Result.Failure<Answer>(GameErrors.InvalidAnswer);

        var optionBelongsToQuestion = question.AnswerOptions.Any(o => o.Id == answerOptionId);
        if (!optionBelongsToQuestion)
            return Result.Failure<Answer>(GameErrors.InvalidAnswer);

        // Step 5: ValidateTime
        var elapsed = serverTimestamp - currentRound.StartedAt;
        var timeRule = new ValidateTimeRule(elapsed, currentRound.TimeLimit);
        if (timeRule.IsBroken())
        {
            var expiredAnswer = CreateExpiredAnswer(playerId, currentRound, question, answerOptionId, currentRound.TimeLimit);
            _answers.Add(expiredAnswer);
            RaiseDomainEvent(new AnswerSubmittedDomainEvent(
                Id.Value, expiredAnswer.Id.Value, playerId,
                currentRound.Id.Value, currentRound.QuestionId.Value, answerOptionId.Value));
            return Result.Failure<Answer>(GameErrors.AnswerTimeout);
        }

        // Step 6: ValidateIdempotency
        var existingAnswer = _answers.FirstOrDefault(a =>
            a.PlayerId == playerId && a.RoundId == currentRound.Id);
        if (existingAnswer is not null)
            return Result.Success(existingAnswer);

        // Step 7: EvaluateAnswer
        var correctOption = question.AnswerOptions.First(o => o.Id == answerOptionId);
        var isCorrect = correctOption.IsCorrect;
        var elapsedTime = (int)elapsed.TotalSeconds;

        // CalculateResult — PointsPerRound × DifficultyMultiplier
        var points = isCorrect ? ComputeRoundPoints(currentRound.Difficulty) : 0;

        // Create Answer
        var answer = new Answer(
            AnswerId.New(),
            Id,
            playerId,
            currentRound.Id,
            currentRound.QuestionId,
            answerOptionId);

        answer.Submit();
        answer.Evaluate(isCorrect, points, elapsedTime);
        _answers.Add(answer);

        // Scoring via ledger operations (SPEC-007)
        if (isCorrect)
        {
            AwardPointsInternal(player, points, PointTransactionType.AnswerCorrect,
                currentRound.Id, currentRound.QuestionId, answer.Id, roundScoped: true);
        }
        else
        {
            RemovePointsInternal(player, PointTransactionType.AnswerIncorrect,
                currentRound.Id, currentRound.QuestionId, answer.Id);
        }

        RaiseDomainEvent(new AnswerSubmittedDomainEvent(
            Id.Value, answer.Id.Value, playerId,
            currentRound.Id.Value, currentRound.QuestionId.Value, answerOptionId.Value));
        RaiseDomainEvent(new AnswerEvaluatedDomainEvent(
            Id.Value, answer.Id.Value, playerId,
            currentRound.Id.Value, isCorrect, points, elapsedTime, answer.Status));

        return Result.Success(answer);
    }

    public int GetScore(Guid playerId)
    {
        return _pointTransactions
            .Where(pt => pt.PlayerId == playerId)
            .Sum(pt => pt.Points);
    }

    public PlayerScore GetPlayerScore(Guid playerId)
    {
        var player = _players.FirstOrDefault(p => p.UserId == playerId);
        return player?.Score ?? PlayerScore.Zero();
    }

    // ─── SPEC-007: Scoring Domain Operations ───────────────────────────────────────

    public Result<PointTransaction> AwardPoints(
        Guid playerId, int amount, PointTransactionType type,
        GameRoundId? roundId = null, QuestionId? questionId = null,
        AnswerId? answerId = null, string? reason = null, bool roundScoped = false)
    {
        if (amount <= 0)
            return Result.Failure<PointTransaction>(GameErrors.InvalidAdjustmentAmount);

        var stateRule = new ScoringStateValidRule(Status);
        if (stateRule.IsBroken())
            return Result.Failure<PointTransaction>(GameErrors.InvalidScoringState);

        var player = _players.FirstOrDefault(p => p.UserId == playerId);
        if (player is null)
            return Result.Failure<PointTransaction>(GameErrors.PlayerNotInGame);

        var withdrawnRule = new PlayerNotWithdrawnRule(!player.IsActive);
        if (withdrawnRule.IsBroken())
            return Result.Failure<PointTransaction>(GameErrors.PlayerAlreadyWithdrawn);

        var transaction = AwardPointsInternal(player, amount, type, roundId, questionId, answerId, roundScoped, reason);
        return Result.Success(transaction);
    }

    public Result<PointTransaction> RemovePoints(
        Guid playerId, PointTransactionType type,
        GameRoundId? roundId = null, QuestionId? questionId = null,
        AnswerId? answerId = null, string? reason = null)
    {
        var stateRule = new ScoringStateValidRule(Status);
        if (stateRule.IsBroken())
            return Result.Failure<PointTransaction>(GameErrors.InvalidScoringState);

        var player = _players.FirstOrDefault(p => p.UserId == playerId);
        if (player is null)
            return Result.Failure<PointTransaction>(GameErrors.PlayerNotInGame);

        var withdrawnRule = new PlayerNotWithdrawnRule(!player.IsActive);
        if (withdrawnRule.IsBroken())
            return Result.Failure<PointTransaction>(GameErrors.PlayerAlreadyWithdrawn);

        var transaction = RemovePointsInternal(player, type, roundId, questionId, answerId, reason);
        return Result.Success(transaction);
    }

    public Result SecurePoints(Guid playerId)
    {
        var player = _players.FirstOrDefault(p => p.UserId == playerId);
        if (player is null)
            return Result.Failure(GameErrors.PlayerNotInGame);

        var withdrawnRule = new PlayerNotWithdrawnRule(!player.IsActive);
        if (withdrawnRule.IsBroken())
            return Result.Failure(GameErrors.PlayerAlreadyWithdrawn);

        if (player.Score.RoundPoints == 0)
            return Result.Success();

        var securedAmount = player.Score.RoundPoints;
        player.UpdateScore(player.Score.Secure());
        RaiseDomainEvent(new PointsSecuredDomainEvent(Id.Value, playerId, securedAmount, player.Score.SecuredPoints));
        return Result.Success();
    }

    public Result<PointTransaction> ConsumePoints(Guid playerId, int amount, string reason)
    {
        if (amount <= 0)
            return Result.Failure<PointTransaction>(GameErrors.InvalidAdjustmentAmount);

        var player = _players.FirstOrDefault(p => p.UserId == playerId);
        if (player is null)
            return Result.Failure<PointTransaction>(GameErrors.PlayerNotInGame);

        var balanceRule = new SufficientBalanceRule(player.Score.CurrentPoints, amount);
        if (balanceRule.IsBroken())
            return Result.Failure<PointTransaction>(GameErrors.InsufficientPoints);

        player.UpdateScore(player.Score.Consume(amount));
        var transaction = CreateTransaction(player, -amount, PointTransactionType.RewardRedemption, null, null, null, reason);
        RaiseDomainEvent(new ScoreUpdatedDomainEvent(Id.Value, playerId, -amount, player.Score.CurrentPoints, PointTransactionType.RewardRedemption.Name));
        return Result.Success(transaction);
    }

    public Result<PointTransaction> WithdrawPlayer(Guid playerId)
    {
        // Step 1: ValidateGameState — no withdrawal from terminal games
        if (Status.IsTerminal)
            return Result.Failure<PointTransaction>(GameErrors.InvalidGameStateDetail($"Cannot withdraw from terminal game in {Status.Name}"));

        // Step 2: ValidatePlayer
        var player = _players.FirstOrDefault(p => p.UserId == playerId);
        if (player is null)
            return Result.Failure<PointTransaction>(GameErrors.PlayerNotInGame);

        // Step 3: No double withdrawal
        if (player.IsWithdrawn)
            return Result.Failure<PointTransaction>(GameErrors.PlayerAlreadyWithdrawn);

        // Step 4: No withdrawal after elimination
        var eliminatedRule = new PlayerAlreadyEliminatedRule(player.ParticipationStatus);
        if (eliminatedRule.IsBroken())
            return Result.Failure<PointTransaction>(GameErrors.PlayerAlreadyEliminated);

        // Step 5: Participation must still be active
        var participationRule = new ParticipationAlreadyFinishedRule(player.ParticipationStatus);
        if (participationRule.IsBroken())
            return Result.Failure<PointTransaction>(GameErrors.ParticipationAlreadyFinished);

        // CalculateSecuredPoints — apply withdrawal policy
        var strategy = WithdrawalPolicyStrategyFactory.Resolve(Configuration.WithdrawalPolicy);
        var deduction = strategy.CalculateDeduction(player.Score);

        if (deduction > 0)
            player.UpdateScore(player.Score.Deduct(deduction));

        // PlayerWithdrawn + FinishPlayerParticipation
        player.MarkWithdrawn();

        var transaction = CreateTransaction(player, -deduction, PointTransactionType.Withdrawal, null, null, null, $"Withdrawal policy: {strategy.Name}");
        RaiseDomainEvent(new ScoreUpdatedDomainEvent(Id.Value, playerId, -deduction, player.Score.CurrentPoints, PointTransactionType.Withdrawal.Name));
        RaiseDomainEvent(new PlayerWithdrawnDomainEvent(Id.Value, playerId, player.Score.CurrentPoints, strategy.Name));
        return Result.Success(transaction);
    }

    public Result EliminatePlayer(Guid playerId, string reason)
    {
        if (Status.IsTerminal)
            return Result.Failure(GameErrors.InvalidGameStateDetail($"Cannot eliminate from terminal game in {Status.Name}"));

        var player = _players.FirstOrDefault(p => p.UserId == playerId);
        if (player is null)
            return Result.Failure(GameErrors.PlayerNotInGame);

        if (player.IsWithdrawn)
            return Result.Failure(GameErrors.PlayerAlreadyWithdrawn);

        var eliminatedRule = new PlayerAlreadyEliminatedRule(player.ParticipationStatus);
        if (eliminatedRule.IsBroken())
            return Result.Failure(GameErrors.PlayerAlreadyEliminated);

        var participationRule = new ParticipationAlreadyFinishedRule(player.ParticipationStatus);
        if (participationRule.IsBroken())
            return Result.Failure(GameErrors.ParticipationAlreadyFinished);

        player.MarkEliminated();
        RaiseDomainEvent(new PlayerEliminatedDomainEvent(Id.Value, playerId, reason));
        return Result.Success();
    }

    public Result<PointTransaction> AdjustPoints(Guid playerId, int amount, string reason, Guid adminUserId)
    {
        if (amount == 0)
            return Result.Failure<PointTransaction>(GameErrors.InvalidAdjustmentAmount);

        var reasonRule = new AdjustmentReasonRequiredRule(reason);
        if (reasonRule.IsBroken())
            return Result.Failure<PointTransaction>(GameErrors.AdjustmentReasonRequired);

        var player = _players.FirstOrDefault(p => p.UserId == playerId);
        if (player is null)
            return Result.Failure<PointTransaction>(GameErrors.PlayerNotInGame);

        if (amount < 0)
        {
            var balanceRule = new BalanceCannotGoNegativeRule(player.Score.CurrentPoints, -amount);
            if (balanceRule.IsBroken())
                return Result.Failure<PointTransaction>(GameErrors.InsufficientPoints);
        }

        if (amount > 0)
            player.UpdateScore(player.Score.Award(amount, roundScoped: false));
        else
            player.UpdateScore(player.Score.Deduct(-amount));

        var transaction = CreateTransaction(player, amount, PointTransactionType.Adjustment, null, null, null, reason.Trim());
        RaiseDomainEvent(new ScoreUpdatedDomainEvent(Id.Value, playerId, amount, player.Score.CurrentPoints, PointTransactionType.Adjustment.Name));
        return Result.Success(transaction);
    }

    public Result<PointTransaction> RefundPoints(Guid playerId, int amount, string reason)
    {
        if (amount <= 0)
            return Result.Failure<PointTransaction>(GameErrors.InvalidAdjustmentAmount);

        var player = _players.FirstOrDefault(p => p.UserId == playerId);
        if (player is null)
            return Result.Failure<PointTransaction>(GameErrors.PlayerNotInGame);

        player.UpdateScore(player.Score.Award(amount, roundScoped: false));

        var transaction = CreateTransaction(player, amount, PointTransactionType.Adjustment, null, null, null, reason.Trim());
        RaiseDomainEvent(new ScoreUpdatedDomainEvent(Id.Value, playerId, amount, player.Score.CurrentPoints, PointTransactionType.Adjustment.Name));
        return Result.Success(transaction);
    }

    // ─── Internal scoring helpers ──────────────────────────────────────────────────

    private PointTransaction AwardPointsInternal(
        GamePlayer player, int amount, PointTransactionType type,
        GameRoundId? roundId, QuestionId? questionId, AnswerId? answerId,
        bool roundScoped, string? reason = null)
    {
        player.UpdateScore(player.Score.Award(amount, roundScoped));
        var transaction = CreateTransaction(player, amount, type, roundId, questionId, answerId, reason);
        RaiseDomainEvent(new ScoreUpdatedDomainEvent(Id.Value, player.UserId, amount, player.Score.CurrentPoints, type.Name));
        return transaction;
    }

    private PointTransaction RemovePointsInternal(
        GamePlayer player, PointTransactionType type,
        GameRoundId? roundId, QuestionId? questionId, AnswerId? answerId,
        string? reason = null)
    {
        var strategy = LossPolicyStrategyFactory.Resolve(Configuration.LossPolicy);
        var deduction = strategy.CalculateDeduction(player.Score);

        if (deduction > 0)
            player.UpdateScore(player.Score.Deduct(deduction));

        var transaction = CreateTransaction(player, -deduction, type, roundId, questionId, answerId, reason ?? $"Loss policy: {strategy.Name}");
        RaiseDomainEvent(new ScoreUpdatedDomainEvent(Id.Value, player.UserId, -deduction, player.Score.CurrentPoints, type.Name));
        return transaction;
    }

    private PointTransaction CreateTransaction(
        GamePlayer player, int points, PointTransactionType type,
        GameRoundId? roundId, QuestionId? questionId, AnswerId? answerId, string? reason)
    {
        var transaction = new PointTransaction(
            PointTransactionId.New(),
            Id,
            player.UserId,
            roundId,
            questionId,
            answerId,
            type,
            points,
            player.Score.CurrentPoints,
            reason);
        _pointTransactions.Add(transaction);
        return transaction;
    }

    private int ComputeRoundPoints(int difficulty)
    {
        var difficultyMultiplier = 1.0 + (difficulty - 1) * 0.25;
        return (int)(Configuration.PointsPerRound * difficultyMultiplier);
    }

    public Answer? GetAnswer(Guid playerId, GameRoundId roundId)
    {
        return _answers.FirstOrDefault(a =>
            a.PlayerId == playerId && a.RoundId == roundId);
    }

    private Answer CreateExpiredAnswer(
        Guid playerId,
        GameRound round,
        Question question,
        AnswerOptionId answerOptionId,
        int timeLimit)
    {
        var answer = new Answer(
            AnswerId.New(),
            Id,
            playerId,
            round.Id,
            round.QuestionId,
            answerOptionId);

        answer.Expire(timeLimit);

        RaiseDomainEvent(new AnswerSubmittedDomainEvent(
            Id.Value, answer.Id.Value, playerId,
            round.Id.Value, round.QuestionId.Value, answerOptionId.Value));

        return answer;
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
