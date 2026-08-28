using BuildingBlocks.CQRS.Abstractions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.ValueObjects;
using OroQuizClash.Infrastructure.Persistence;

namespace OroQuizClash.Infrastructure.Tests.Persistence;

public sealed class GameConcurrencyTests
{
    private static OroQuizClashDbContext CreateSqliteContext(string dbName)
    {
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        var options = new DbContextOptionsBuilder<OroQuizClashDbContext>()
            .UseSqlite($"Data Source={dbName};Mode=Memory;Cache=Shared")
            .Options;
        var ctx = new OroQuizClashDbContext(options, dispatcher);
        ctx.Database.OpenConnection();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static Question CreateQuestion(CategoryId categoryId) =>
        Question.Create(
            "Test question?",
            categoryId,
            Domain.Questions.ValueObjects.DifficultyLevel.Basic,
            AcademicLevel.Create("Primary"),
            AgeRange.Create(6, 10),
            new (string text, bool isCorrect, int displayOrder)[]
            {
                ("Correct answer", true, 0),
                ("Wrong B", false, 1),
                ("Wrong C", false, 2),
                ("Wrong D", false, 3)
            },
            Guid.NewGuid()).Value;

    private static Game SeedGameWithActiveRound(OroQuizClashDbContext ctx, out Guid player1, out Guid player2, out Question question)
    {
        var config = new GameConfiguration(
            "Multiplayer Quiz",
            new CategoryId(Guid.NewGuid()),
            5, 10, 1,
            DifficultyProgressionStrategy.Linear, 30,
            ScoringSystem.Standard, LossPolicy.LoseAll,
            WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None,
            new RewardRules("Points", 500),
            2, 10, 100);

        var game = Game.Create(config, Guid.NewGuid()).Value;
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        player1 = Guid.NewGuid();
        player2 = Guid.NewGuid();
        game.JoinPlayer(player1, "Alice");
        game.JoinPlayer(player2, "Bob");
        game.Start();
        question = CreateQuestion(config.CategoryId);
        game.StartRound(question.Id.Value, 1);

        ctx.Games.Add(game);
        ctx.SaveChanges();
        return game;
    }

    private static async Task<Game> LoadGameAsync(OroQuizClashDbContext ctx, GameId gameId)
    {
        var game = await ctx.Games
            .Include(g => g.Players)
            .Include(g => g.Rounds)
            .Include(g => g.Answers)
            .Include(g => g.PointTransactions)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        Assert.NotNull(game);
        return game!;
    }

    [Fact]
    public async Task SimultaneousSubmissions_DifferentPlayers_AllPersisted_NoLostUpdates()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var seedCtx = CreateSqliteContext(dbName);
        var seeded = SeedGameWithActiveRound(seedCtx, out var player1, out var player2, out var question);
        var gameId = seeded.Id;
        var questionId = question.Id;
        var correctOption = question.AnswerOptions.First(o => o.IsCorrect).Id;

        // Player 1 submits (request processed first)
        await using (var ctx1 = CreateSqliteContext(dbName))
        {
            var game1 = await LoadGameAsync(ctx1, gameId);
            var result1 = game1.SubmitAnswer(player1, correctOption, DateTimeOffset.UtcNow, qid => qid == questionId ? question : null);
            Assert.True(result1.IsSuccess);
            await ctx1.SaveChangesAsync();
        }

        // Player 2 submits concurrently (request processed second, loads fresh state)
        await using (var ctx2 = CreateSqliteContext(dbName))
        {
            var game2 = await LoadGameAsync(ctx2, gameId);
            var result2 = game2.SubmitAnswer(player2, correctOption, DateTimeOffset.UtcNow, qid => qid == questionId ? question : null);
            Assert.True(result2.IsSuccess);
            await ctx2.SaveChangesAsync();
        }

        // Verify: both answers and transactions persisted, no lost or duplicated updates
        await using var verifyCtx = CreateSqliteContext(dbName);
        var game = await LoadGameAsync(verifyCtx, gameId);

        Assert.Equal(2, game.Answers.Count);
        Assert.Single(game.Answers, a => a.PlayerId == player1);
        Assert.Single(game.Answers, a => a.PlayerId == player2);
        Assert.All(game.Answers, a => Assert.Equal(AnswerStatus.Evaluated, a.Status));

        foreach (var playerId in new[] { player1, player2 })
        {
            var ledgerSum = game.PointTransactions.Where(pt => pt.PlayerId == playerId).Sum(pt => pt.Points);
            var player = game.Players.Single(p => p.UserId == playerId);
            Assert.Equal(ledgerSum, player.Score.CurrentPoints);
            Assert.True(ledgerSum > 0);
        }
    }

    [Fact]
    public async Task SimultaneousSubmissions_EachPlayerGetsOwnResult_NoCrossContamination()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var seedCtx = CreateSqliteContext(dbName);
        var seeded = SeedGameWithActiveRound(seedCtx, out var player1, out var player2, out var question);
        var gameId = seeded.Id;
        var questionId = question.Id;
        var correctOption = question.AnswerOptions.First(o => o.IsCorrect).Id;
        var wrongOption = question.AnswerOptions.First(o => !o.IsCorrect).Id;

        await using (var ctx1 = CreateSqliteContext(dbName))
        {
            var game1 = await LoadGameAsync(ctx1, gameId);
            var result1 = game1.SubmitAnswer(player1, correctOption, DateTimeOffset.UtcNow, qid => qid == questionId ? question : null);
            Assert.True(result1.IsSuccess);
            Assert.True(result1.Value.Correct);
            await ctx1.SaveChangesAsync();
        }

        await using (var ctx2 = CreateSqliteContext(dbName))
        {
            var game2 = await LoadGameAsync(ctx2, gameId);
            var result2 = game2.SubmitAnswer(player2, wrongOption, DateTimeOffset.UtcNow, qid => qid == questionId ? question : null);
            Assert.True(result2.IsSuccess);
            Assert.False(result2.Value.Correct);
            await ctx2.SaveChangesAsync();
        }

        await using var verifyCtx = CreateSqliteContext(dbName);
        var game = await LoadGameAsync(verifyCtx, gameId);

        var answer1 = game.Answers.Single(a => a.PlayerId == player1);
        var answer2 = game.Answers.Single(a => a.PlayerId == player2);
        Assert.True(answer1.Correct);
        Assert.False(answer2.Correct);
        Assert.True(game.Players.Single(p => p.UserId == player1).Score.CurrentPoints > 0);
        Assert.Equal(0, game.Players.Single(p => p.UserId == player2).Score.CurrentPoints);
    }

    [Fact]
    public async Task StaleVersionConflict_SamePlayerMutation_SecondSaveThrowsConcurrencyException()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var seedCtx = CreateSqliteContext(dbName);
        var seeded = SeedGameWithActiveRound(seedCtx, out var player1, out _, out var question);
        var gameId = seeded.Id;
        var questionId = question.Id;
        var correctOption = question.AnswerOptions.First(o => o.IsCorrect).Id;

        // Both requests load the same aggregate version
        await using var ctx1 = CreateSqliteContext(dbName);
        await using var ctx2 = CreateSqliteContext(dbName);
        var game1 = await LoadGameAsync(ctx1, gameId);
        var game2 = await LoadGameAsync(ctx2, gameId);

        // Request 1: withdraw player1 -> commits, bumps aggregate RowVersion
        var withdraw = game1.WithdrawPlayer(player1);
        Assert.True(withdraw.IsSuccess);
        await ctx1.SaveChangesAsync();

        // Request 2 (stale version): mutates the SAME player's state -> loses the race
        var result2 = game2.SubmitAnswer(player1, correctOption, DateTimeOffset.UtcNow, qid => qid == questionId ? question : null);
        Assert.True(result2.IsSuccess);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctx2.SaveChangesAsync());
    }

    [Fact]
    public async Task DuplicateSubmission_SamePlayerSameRound_SingleAnswerSingleTransaction()
    {
        // Path 1: in-memory idempotency — domain returns the existing answer, no duplicate effects
        var dbName1 = Guid.NewGuid().ToString();
        await using var seedCtx1 = CreateSqliteContext(dbName1);
        var seeded1 = SeedGameWithActiveRound(seedCtx1, out var player1, out _, out var question);
        var gameId1 = seeded1.Id;
        var questionId = question.Id;
        var correctOption = question.AnswerOptions.First(o => o.IsCorrect).Id;

        await using (var ctx1 = CreateSqliteContext(dbName1))
        {
            var game1 = await LoadGameAsync(ctx1, gameId1);
            var first = game1.SubmitAnswer(player1, correctOption, DateTimeOffset.UtcNow, qid => qid == questionId ? question : null);
            var second = game1.SubmitAnswer(player1, correctOption, DateTimeOffset.UtcNow, qid => qid == questionId ? question : null);
            Assert.True(first.IsSuccess && second.IsSuccess);
            Assert.Equal(first.Value.Id, second.Value.Id);
            await ctx1.SaveChangesAsync();
        }

        await using (var verifyCtx1 = CreateSqliteContext(dbName1))
        {
            var game = await LoadGameAsync(verifyCtx1, gameId1);
            var answer = game.Answers.Single(a => a.PlayerId == player1);
            Assert.Equal(AnswerStatus.Evaluated, answer.Status);
            Assert.Single(game.PointTransactions.Where(pt => pt.AnswerId == answer.Id));

            // The unique index guard must exist on the model (defense in depth)
            var answerIndex = verifyCtx1.Model.FindEntityType(typeof(Answer))!.GetIndexes()
                .Single(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(["GameId", "PlayerId", "RoundId"]));
            Assert.NotNull(answerIndex);
        }

        // Path 2: racing retry with stale state — rejected before duplicating effects
        // (aggregate RowVersion conflict and/or unique index (GameId, PlayerId, RoundId))
        var dbName2 = Guid.NewGuid().ToString();
        await using var seedCtx2 = CreateSqliteContext(dbName2);
        var seeded2 = SeedGameWithActiveRound(seedCtx2, out var player2, out _, out var question2);
        var gameId2 = seeded2.Id;
        var question2Id = question2.Id;
        var correctOption2 = question2.AnswerOptions.First(o => o.IsCorrect).Id;

        await using var ctxStale = CreateSqliteContext(dbName2);
        var staleGame = await LoadGameAsync(ctxStale, gameId2); // loads before any answer exists

        await using (var ctxRival = CreateSqliteContext(dbName2))
        {
            var rivalGame = await LoadGameAsync(ctxRival, gameId2);
            var rivalResult = rivalGame.SubmitAnswer(player2, correctOption2, DateTimeOffset.UtcNow, qid => qid == question2Id ? question2 : null);
            Assert.True(rivalResult.IsSuccess);
            await ctxRival.SaveChangesAsync();
        }

        // Stale view has no answer for player2 -> domain creates a new Answer entity;
        // persistence must reject it (stale RowVersion and/or unique index violation)
        var duplicate = staleGame.SubmitAnswer(player2, correctOption2, DateTimeOffset.UtcNow, qid => qid == question2Id ? question2 : null);
        Assert.True(duplicate.IsSuccess);
        var ex = await Record.ExceptionAsync(() => ctxStale.SaveChangesAsync());
        Assert.NotNull(ex);
        Assert.True(ex is DbUpdateConcurrencyException or DbUpdateException, $"unexpected exception {ex!.GetType().Name}");

        // Final state: exactly one Answer and one PointTransaction for the round-1 submission
        await using var verifyCtx2 = CreateSqliteContext(dbName2);
        var finalGame = await LoadGameAsync(verifyCtx2, gameId2);
        var finalAnswer = finalGame.Answers.Single(a => a.PlayerId == player2);
        Assert.Single(finalGame.PointTransactions.Where(pt => pt.AnswerId == finalAnswer.Id));
    }

    [Fact]
    public async Task Atomicity_EveryEvaluatedAnswer_HasExactlyOneTransaction_BalancesMatchLedger()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var seedCtx = CreateSqliteContext(dbName);
        var seeded = SeedGameWithActiveRound(seedCtx, out var player1, out var player2, out var question);
        var gameId = seeded.Id;
        var questionId = question.Id;
        var correctOption = question.AnswerOptions.First(o => o.IsCorrect).Id;
        var wrongOption = question.AnswerOptions.First(o => !o.IsCorrect).Id;

        await using (var ctx1 = CreateSqliteContext(dbName))
        {
            var game1 = await LoadGameAsync(ctx1, gameId);
            game1.SubmitAnswer(player1, correctOption, DateTimeOffset.UtcNow, qid => qid == questionId ? question : null);
            game1.SubmitAnswer(player2, wrongOption, DateTimeOffset.UtcNow, qid => qid == questionId ? question : null);
            await ctx1.SaveChangesAsync();
        }

        await using var verifyCtx = CreateSqliteContext(dbName);
        var game = await LoadGameAsync(verifyCtx, gameId);

        foreach (var answer in game.Answers.Where(a => a.Status == AnswerStatus.Evaluated))
        {
            var linked = game.PointTransactions.Count(pt => pt.AnswerId == answer.Id);
            Assert.Equal(1, linked);
        }

        foreach (var player in game.Players)
        {
            var ledgerSum = game.PointTransactions.Where(pt => pt.PlayerId == player.UserId).Sum(pt => pt.Points);
            Assert.Equal(ledgerSum, player.Score.CurrentPoints);
        }

        // No orphan transactions: every transaction for an answer references an existing evaluated answer
        Assert.All(game.PointTransactions.Where(pt => pt.AnswerId != null),
            pt => Assert.Contains(game.Answers, a => a.Id == pt.AnswerId && a.Status == AnswerStatus.Evaluated));
    }
}
