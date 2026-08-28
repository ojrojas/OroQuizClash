using Microsoft.EntityFrameworkCore;

using NSubstitute;

using OroQuizClash.Application.Features.Audit;
using OroQuizClash.Domain.Audit;
using OroQuizClash.Infrastructure.Persistence;

namespace OroQuizClash.Application.Tests.Features.Audit;

public sealed class GetAuditEntriesSearchTests
{
    [Fact]
    public async Task Search_ByGameId_ReturnsFiltered()
    {
        var gameId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<OroQuizClashDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var dispatcher = Substitute.For<BuildingBlocks.CQRS.Abstractions.IDomainEventDispatcher>();
        var db = new OroQuizClashDbContext(options, dispatcher);
        db.AuditEntries.Add(AuditEntry.Create(DateTimeOffset.UtcNow, "actor", "ADMIN", "GameCreated", "Game.Create", "Game", gameId.ToString(), gameId, null, "corr1", null, "Succeeded", null, "{}"));
        db.AuditEntries.Add(AuditEntry.Create(DateTimeOffset.UtcNow, "actor", "ADMIN", "GameCreated", "Game.Create", "Game", Guid.NewGuid().ToString(), Guid.NewGuid(), null, "corr2", null, "Succeeded", null, "{}"));
        await db.SaveChangesAsync();
        var repo = new BuildingBlocks.Kernel.Infrastructure.Persistence.EfRepository<AuditEntry, Guid>(db);
        var handler = new GetAuditEntriesHandler(repo);
        var result = await handler.HandleAsync(new GetAuditEntriesQuery(null, null, null, null, null, gameId, null, null, null, null, 1, 20), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(gameId, result.Value.Items[0].GameId);
    }

    [Fact]
    public async Task Search_ByCorrelationId_ReturnsOrdered()
    {
        var corr = "corr-trace-123";
        var options = new DbContextOptionsBuilder<OroQuizClashDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var dispatcher = Substitute.For<BuildingBlocks.CQRS.Abstractions.IDomainEventDispatcher>();
        var db = new OroQuizClashDbContext(options, dispatcher);
        db.AuditEntries.Add(AuditEntry.Create(DateTimeOffset.UtcNow.AddSeconds(-2), "actor", "PLAYER", "AnswerSubmitted", "Game.Play", "Answer", null, Guid.NewGuid(), Guid.NewGuid(), corr, null, "Succeeded", null, "{}"));
        db.AuditEntries.Add(AuditEntry.Create(DateTimeOffset.UtcNow.AddSeconds(-1), "actor", "PLAYER", "AnswerEvaluated", "Game.Play", "Answer", null, Guid.NewGuid(), Guid.NewGuid(), corr, null, "Succeeded", null, "{}"));
        await db.SaveChangesAsync();
        var repo = new BuildingBlocks.Kernel.Infrastructure.Persistence.EfRepository<AuditEntry, Guid>(db);
        var handler = new GetAuditEntriesHandler(repo);
        var result = await handler.HandleAsync(new GetAuditEntriesQuery(corr, null, null, null, null, null, null, null, null, null, 1, 20), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
    }
}
