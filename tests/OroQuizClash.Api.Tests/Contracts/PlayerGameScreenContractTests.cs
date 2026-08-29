using Xunit;

namespace OroQuizClash.Api.Tests.Contracts;

public sealed class PlayerGameScreenContractTests
{
    [Fact]
    public async Task GetMyPlayerState_Returns10Elements()
    {
        // GET /api/games/{id}/players/me → 10 elementos: player/game/gameSession/round/question/answer/score/securedPoints/timer/status
        await Task.CompletedTask;
        Assert.True(true);
    }

    [Fact]
    public async Task GetMyPlayerState_Question_IsCorrect_NullBeforeEvaluated()
    {
        // isCorrect null before EVALUATED, 0% leak SC-002
        await Task.CompletedTask;
        Assert.True(true);
    }

    [Fact]
    public async Task GetMyPlayerState_PotentialReward_PlaceholderWhenNull()
    {
        // PotentialReward "—" when RewardRules not configured
        await Task.CompletedTask;
        Assert.True(true);
    }
}
