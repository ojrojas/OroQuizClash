using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using NSubstitute;

using OroQuizClash.Application.Features.Games;

namespace OroQuizClash.Application.Tests.Features.Games;

public sealed class SubmitAnswerAuthorityTests
{
    [Fact]
    public void SubmitAnswerCommand_OnlyAnswerOptionId_IsExposed()
    {
        var cmd = new SubmitAnswerCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null);
        var props = typeof(SubmitAnswerCommand).GetProperties().Select(p => p.Name).ToList();
        Assert.Contains("AnswerOptionId", props);
        Assert.Contains("GameId", props);
        Assert.Contains("PlayerId", props);
        Assert.DoesNotContain("Score", props);
        Assert.DoesNotContain("Correctness", props);
        Assert.DoesNotContain("GameState", props);
    }

    [Fact]
    public void GameClaims_GetSub_ReturnsAuthenticatedPlayerId()
    {
        var playerId = Guid.NewGuid();
        var claims = new List<Claim> { new("sub", playerId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var httpContext = new DefaultHttpContext { User = principal };
        var resolved = GameClaims.GetSub(httpContext.User);
        Assert.Equal(playerId, resolved);
    }
}
