using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Infrastructure.Persistence;
using CategoryDomain = OroQuizClash.Domain.Categories.Category;
using CategoryId = OroQuizClash.Domain.Categories.CategoryId;
using CategoryAgeRange = OroQuizClash.Domain.Categories.ValueObjects.AgeRange;
using CategoryDifficulty = OroQuizClash.Domain.Categories.ValueObjects.DifficultyLevel;
using CategoryAcademicLevel = OroQuizClash.Domain.Categories.ValueObjects.AcademicLevel;
using CategoryKnowledgeArea = OroQuizClash.Domain.Categories.ValueObjects.KnowledgeArea;
using CategoryTags = OroQuizClash.Domain.Categories.ValueObjects.CategoryTags;
using QuestionDomain = OroQuizClash.Domain.Questions.Question;
using QuestionAgeRange = OroQuizClash.Domain.Questions.ValueObjects.AgeRange;
using QuestionAcademicLevel = OroQuizClash.Domain.Questions.ValueObjects.AcademicLevel;
using QuestionDifficulty = OroQuizClash.Domain.Questions.ValueObjects.DifficultyLevel;
using QuestionStatus = OroQuizClash.Domain.Questions.QuestionStatus;

namespace OroQuizClash.Seeder;

public sealed class Worker(
    ILogger<Worker> logger,
    IServiceProvider sp,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);
            await SeedAsync(stoppingToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Seeder falló");
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OroQuizClashDbContext>();

        var retry = 0;
        while (true)
        {
            try
            {
                await db.Database.EnsureCreatedAsync(ct);
                break;
            }
            catch (Exception ex) when (retry < 5)
            {
                retry++;
                logger.LogWarning(ex, "EnsureCreated reintento {Retry}/5", retry);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }

        var existingCats = await db.Categories.CountAsync(ct);
        if (existingCats >= 10)
        {
            logger.LogInformation("Seed skip: ya existen {Count} categorías", existingCats);
            var gamesCount = await db.Games.CountAsync(ct);
            logger.LogInformation("Juegos existentes: {Count}", gamesCount);
            return;
        }

        logger.LogInformation("Iniciando seeder: 10 categorías × 20 preguntas + 10 juegos WAITING_FOR_PLAYERS");

        var createdBy = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var categoryIds = new Dictionary<string, CategoryId>(StringComparer.OrdinalIgnoreCase);

        foreach (var catSeed in SeedData.Categories)
        {
            var existing = await db.Categories.FirstOrDefaultAsync(c => c.Name == catSeed.Name, ct);
            if (existing is not null)
            {
                categoryIds[catSeed.Name] = existing.Id;
                logger.LogInformation("Categoría ya existe: {Name}", catSeed.Name);
                continue;
            }

            var ka = new CategoryKnowledgeArea(catSeed.KnowledgeArea);
            var al = new CategoryAcademicLevel(catSeed.AcademicLevel);
            var age = new CategoryAgeRange(catSeed.AgeMin, catSeed.AgeMax);
            var diff = new CategoryDifficulty(catSeed.Difficulty);
            var tags = new CategoryTags(catSeed.Tags);
            var pub = new OroQuizClash.Domain.Categories.ValueObjects.PublishConfiguration(false);

            var res = CategoryDomain.Create(catSeed.Name, catSeed.Description, ka, al, age, diff, tags, pub, createdBy);
            if (res.IsFailure)
            {
                logger.LogWarning("No se pudo crear categoría {Name}: {Err}", catSeed.Name, res.Error);
                continue;
            }

            db.Categories.Add(res.Value!);
            await db.SaveChangesAsync(ct);
            categoryIds[catSeed.Name] = res.Value!.Id;
            logger.LogInformation("Categoría creada: {Name} {Id}", catSeed.Name, res.Value!.Id.Value);
        }

        var rng = new Random(42);
        foreach (var catSeed in SeedData.Categories)
        {
            if (!categoryIds.TryGetValue(catSeed.Name, out var catId)) continue;
            if (!SeedData.QuestionsByCategory.TryGetValue(catSeed.Name, out var qSeeds)) continue;

            var existingQ = await db.Questions.CountAsync(q => q.CategoryId == catId, ct);
            if (existingQ >= 20)
            {
                logger.LogInformation("Preguntas ya existen para {Cat}: {Count}", catSeed.Name, existingQ);
                continue;
            }

            var toCreate = qSeeds.Skip(existingQ).Take(20 - existingQ).ToList();
            if (existingQ > 0) logger.LogInformation("Completando {Cat}: faltan {N}", catSeed.Name, toCreate.Count);

            foreach (var qSeed in toCreate)
            {
                var diff = QuestionDifficulty.FromId(qSeed.Difficulty);
                var al = new QuestionAcademicLevel(qSeed.AcademicLevel);
                var age = new QuestionAgeRange(qSeed.AgeMin, qSeed.AgeMax);
                var opts = qSeed.Options.Select((t, i) => (text: t, isCorrect: i == qSeed.CorrectIndex, displayOrder: i)).ToList();

                var qRes = QuestionDomain.Create(qSeed.Text, catId, diff, al, age, opts, createdBy);
                if (qRes.IsFailure)
                {
                    logger.LogWarning("Pregunta falló {Cat}: {Err} {Text}", catSeed.Name, qRes.Error, qSeed.Text[..Math.Min(40, qSeed.Text.Length)]);
                    continue;
                }

                var q = qRes.Value!;
                var pubRes = q.Publish();
                if (pubRes.IsFailure)
                {
                    logger.LogWarning("Publish pregunta falló {Text}: {Err}", qSeed.Text[..Math.Min(20, qSeed.Text.Length)], pubRes.Error);
                }

                db.Questions.Add(q);
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Preguntas creadas para {Cat}: {Count}", catSeed.Name, toCreate.Count);

            var cat = await db.Categories.FirstAsync(c => c.Id == catId, ct);
            var validCount = await db.Questions.CountAsync(q => q.CategoryId == catId && q.Status == QuestionStatus.Published, ct);
            if (validCount >= 5 && cat.Status != CategoryStatus.Active)
            {
                var counter = new EfCount(catId, validCount);
                var pubCat = await cat.PublishAsync(counter, ct);
                if (pubCat.IsSuccess)
                {
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation("Categoría publicada Active: {Name} ({Count} válidas)", catSeed.Name, validCount);
                }
                else
                {
                    logger.LogWarning("No se pudo publicar {Name}: {Err}", catSeed.Name, pubCat.Error);
                }
            }
        }

        var existingGamesCount = await db.Games.CountAsync(ct);
        if (existingGamesCount >= 10)
        {
            logger.LogInformation("Juegos ya existen: {Count}, skip", existingGamesCount);
            return;
        }

        var gameIdx = 0;
        foreach (var catSeed in SeedData.Categories)
        {
            if (!categoryIds.TryGetValue(catSeed.Name, out var catId)) continue;
            var cat = await db.Categories.FirstOrDefaultAsync(c => c.Id == catId, ct);
            if (cat is null || cat.Status != CategoryStatus.Active) continue;

            var gameName = $"Torneo {catSeed.Name} - Secundaria {++gameIdx:00}";
            if (await db.Games.AnyAsync(g => g.Configuration.Name == gameName, ct)) continue;

            var config = new GameConfiguration(
                name: gameName,
                categoryId: catId,
                minRounds: 5,
                maxRounds: 8,
                initialDifficulty: rng.Next(1, 4),
                difficultyStrategy: DifficultyProgressionStrategy.Linear,
                timeLimitPerQuestionSeconds: 30,
                scoringSystem: ScoringSystem.Standard,
                lossPolicy: LossPolicy.LoseUnsecuredPoints,
                withdrawalPolicy: WithdrawalPolicy.KeepSecuredScore,
                consolationPolicy: ConsolationPolicy.None,
                rewardRules: new RewardRules("Points", 1000),
                minPlayers: 2,
                maxPlayers: 10
            );

            var gRes = Game.Create(config, createdBy);
            if (gRes.IsFailure)
            {
                logger.LogWarning("Game.Create falló {Name}: {Err}", gameName, gRes.Error);
                continue;
            }

            var game = gRes.Value!;
            db.Games.Add(game);
            await db.SaveChangesAsync(ct);

            var readyRes = game.MarkReady(_ => true, _ => 20);
            if (readyRes.IsFailure)
            {
                logger.LogWarning("MarkReady falló {Name}: {Err}", gameName, readyRes.Error);
                continue;
            }
            await db.SaveChangesAsync(ct);

            var lobbyRes = game.OpenLobby();
            if (lobbyRes.IsFailure)
            {
                logger.LogWarning("OpenLobby falló {Name}: {Err}", gameName, lobbyRes.Error);
                continue;
            }
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Juego WAITING_FOR_PLAYERS creado: {Name} {Id}", gameName, game.Id.Value);
        }

        var totalGames = await db.Games.CountAsync(ct);
        var waiting = await db.Games.CountAsync(g => g.Status == GameStatus.WaitingForPlayers, ct);
        logger.LogInformation("Seeder completo: {Cats} categorías, 200 preguntas, {Games} juegos ({Waiting} WAITING_FOR_PLAYERS)", categoryIds.Count, totalGames, waiting);
    }

    private sealed class EfCount(CategoryId id, int count) : IQuestionCounter
    {
        public Task<int> CountValidAsync(CategoryId categoryId, CancellationToken ct = default)
            => Task.FromResult(categoryId == id ? count : 0);
    }
}
