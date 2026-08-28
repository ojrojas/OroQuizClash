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

public sealed class AuditBehaviorSanitizationTests
{
    [Fact]
    public async Task Data_DoesNotContainIsCorrectKey()
    {
        var options = new DbContextOptionsBuilder<OroQuizClashDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        var db = new OroQuizClashDbContext(options, dispatcher);
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()), new("role", "PLAYER") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var httpContext = new DefaultHttpContext { User = principal };
        httpAccessor.HttpContext.Returns(httpContext);
        var behavior = new AuditBehavior<FakeSanitizeWithCorrectCommand, Result<Guid>>(httpAccessor, db, NullLogger<AuditBehavior<FakeSanitizeWithCorrectCommand, Result<Guid>>>.Instance);
        var cmd = new FakeSanitizeWithCorrectCommand(Guid.NewGuid(), true);
        await behavior.HandleAsync(cmd, _ => Task.FromResult(Result.Success(Guid.NewGuid())), CancellationToken.None);
        var data = db.AuditEntries.First().Data ?? db.AuditEntries.First().Details ?? "";
        Assert.DoesNotContain("\"IsCorrect\"", data, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record FakeSanitizeWithCorrectCommand(Guid GameId, bool IsCorrect) : ICommand<Result<Guid>>;
public sealed record FakeSanitizeCommand(Guid GameId, string Secret) : ICommand<Result<Guid>>;
