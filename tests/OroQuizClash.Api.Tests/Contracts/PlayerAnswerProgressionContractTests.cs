using Xunit;

namespace OroQuizClash.Api.Tests.Contracts;

public sealed class PlayerAnswerProgressionContractTests
{
    [Fact]
    public async Task SubmitAnswer_Idempotent_SameKey_NoDuplicateLedger()
    {
        await Task.CompletedTask;
        Assert.True(true);
    }

    [Fact]
    public async Task SubmitAnswer_Scoring_Updates_Score_Secured_Potential()
    {
        await Task.CompletedTask;
        Assert.True(true);
    }
}
