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

public sealed class AuditBehaviorScoringTests
{
    [Fact]
    public async Task SubmitAnswer_GeneratesAnswerSubmittedWithSameCorrelationId()
    {
        var options = new DbContextOptionsBuilder<OroQuizClashDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        var db = new OroQuizClashDbContext(options, dispatcher);
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()), new("role", "PLAYER") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-scoring-123";
        httpAccessor.HttpContext.Returns(httpContext);
        var behavior = new AuditBehavior<FakeSubmitAnswerCommand, Result<Guid>>(httpAccessor, db, NullLogger<AuditBehavior<FakeSubmitAnswerCommand, Result<Guid>>>.Instance);
        var cmd = new FakeSubmitAnswerCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = await behavior.HandleAsync(cmd, _ => Task.FromResult(Result.Success(Guid.NewGuid())), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Single(db.AuditEntries);
        Assert.Equal("AnswerSubmitted", db.AuditEntries.First().Action);
        Assert.Equal("corr-scoring-123", db.AuditEntries.First().CorrelationId);
    }
}

public sealed record FakeSubmitAnswerCommand(Guid GameId, Guid PlayerId) : ICommand<Result<Guid>>;
