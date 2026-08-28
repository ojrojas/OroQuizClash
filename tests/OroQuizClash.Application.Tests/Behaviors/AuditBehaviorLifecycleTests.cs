using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using OroQuizClash.Application.Authorization;
using OroQuizClash.Application.Behaviors;
using OroQuizClash.Domain.Audit;
using OroQuizClash.Infrastructure.Persistence;

using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

namespace OroQuizClash.Application.Tests.Behaviors;

[RequiresPermission("Game.Create")]
public sealed record FakeCreateGameCommand(Guid GameId) : ICommand<Result<Guid>>;

[RequiresPermission("Game.Play")]
public sealed record FakeJoinGameCommand(Guid GameId, Guid PlayerId) : ICommand<Result<Guid>>;

public sealed class AuditBehaviorLifecycleTests
{
    private static (AuditBehavior<FakeCreateGameCommand, Result<Guid>> behavior, OroQuizClashDbContext db, HttpContext ctx) CreateBehavior()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<OroQuizClashDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dispatcher = Substitute.For<BuildingBlocks.CQRS.Abstractions.IDomainEventDispatcher>();
        var db = new OroQuizClashDbContext(options, dispatcher);
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()), new("role", "ADMIN") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-lifecycle-123";
        httpAccessor.HttpContext.Returns(httpContext);
        var logger = NullLogger<AuditBehavior<FakeCreateGameCommand, Result<Guid>>>.Instance;
        var behavior = new AuditBehavior<FakeCreateGameCommand, Result<Guid>>(httpAccessor, db, logger);
        return (behavior, db, httpContext);
    }

    [Fact]
    public async Task CreateGame_GeneratesGameCreatedAudit()
    {
        var (behavior, db, _) = CreateBehavior();
        var cmd = new FakeCreateGameCommand(Guid.NewGuid());
        var result = await behavior.HandleAsync(cmd, _ => Task.FromResult(Result.Success(Guid.NewGuid())), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Single(db.AuditEntries);
        Assert.Equal("GameCreated", db.AuditEntries.First().Action);
        Assert.NotNull(db.AuditEntries.First().GameId);
    }

    [Fact]
    public async Task JoinGame_GeneratesPlayerJoinedAudit()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<OroQuizClashDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dispatcher = Substitute.For<BuildingBlocks.CQRS.Abstractions.IDomainEventDispatcher>();
        var db = new OroQuizClashDbContext(options, dispatcher);
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()), new("role", "PLAYER") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-join-123";
        httpAccessor.HttpContext.Returns(httpContext);
        var logger = NullLogger<AuditBehavior<FakeJoinGameCommand, Result<Guid>>>.Instance;
        var behavior = new AuditBehavior<FakeJoinGameCommand, Result<Guid>>(httpAccessor, db, logger);
        var cmd = new FakeJoinGameCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = await behavior.HandleAsync(cmd, _ => Task.FromResult(Result.Success(Guid.NewGuid())), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Single(db.AuditEntries);
        Assert.Equal("PlayerJoined", db.AuditEntries.First().Action);
    }
}
