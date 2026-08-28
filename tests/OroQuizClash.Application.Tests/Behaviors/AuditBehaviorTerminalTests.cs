using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using OroQuizClash.Application.Behaviors;
using OroQuizClash.Domain.Audit;
using OroQuizClash.Infrastructure.Persistence;

using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

namespace OroQuizClash.Application.Tests.Behaviors;

public sealed class AuditBehaviorTerminalTests
{
    [Fact]
    public async Task WithdrawPlayer_GeneratesPlayerWithdrawn()
    {
        var options = new DbContextOptionsBuilder<OroQuizClashDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        var db = new OroQuizClashDbContext(options, dispatcher);
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()), new("role", "PLAYER") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var httpContext = new DefaultHttpContext { User = principal };
        httpAccessor.HttpContext.Returns(httpContext);
        var behavior = new AuditBehavior<FakeWithdrawCommand, Result<Guid>>(httpAccessor, db, NullLogger<AuditBehavior<FakeWithdrawCommand, Result<Guid>>>.Instance);
        var cmd = new FakeWithdrawCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = await behavior.HandleAsync(cmd, _ => Task.FromResult(Result.Success(Guid.NewGuid())), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Single(db.AuditEntries);
        Assert.Equal("PlayerWithdrawn", db.AuditEntries.First().Action);
    }

    [Fact]
    public async Task RedeemReward_GeneratesRewardRedeemed()
    {
        var options = new DbContextOptionsBuilder<OroQuizClashDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        var db = new OroQuizClashDbContext(options, dispatcher);
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()), new("role", "PLAYER") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var httpContext = new DefaultHttpContext { User = principal };
        httpAccessor.HttpContext.Returns(httpContext);
        var behavior = new AuditBehavior<FakeRedeemCommand, Result<Guid>>(httpAccessor, db, NullLogger<AuditBehavior<FakeRedeemCommand, Result<Guid>>>.Instance);
        var cmd = new FakeRedeemCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = await behavior.HandleAsync(cmd, _ => Task.FromResult(Result.Success(Guid.NewGuid())), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Single(db.AuditEntries);
        Assert.Equal("RewardRedeemed", db.AuditEntries.First().Action);
    }
}

public sealed record FakeWithdrawCommand(Guid GameId, Guid PlayerId) : ICommand<Result<Guid>>;
public sealed record FakeRedeemCommand(Guid GameId, Guid RewardId) : ICommand<Result<Guid>>;
