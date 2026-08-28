using OroQuizClash.Infrastructure.Services;

namespace OroQuizClash.Application.Tests.Services;

public sealed class IdempotencyServiceTests
{
    [Fact]
    public void ComputeHash_SamePayload_SameHash()
    {
        var payload = new { gameId = Guid.NewGuid(), playerId = Guid.NewGuid() };
        var h1 = IdempotencyService.ComputeHash(payload);
        var h2 = IdempotencyService.ComputeHash(payload);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void ComputeHash_DifferentPayload_DifferentHash()
    {
        var h1 = IdempotencyService.ComputeHash(new { a = 1 });
        var h2 = IdempotencyService.ComputeHash(new { a = 2 });
        Assert.NotEqual(h1, h2);
    }
}
