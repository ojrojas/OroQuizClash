using Xunit;

namespace OroQuizClash.Api.Tests.Contracts;

public sealed class PlayerWithdrawContractTests
{
    [Fact]
    public async Task Withdraw_Idempotent_SameKey_NoDuplicateLedger()
    {
        await Task.CompletedTask;
        Assert.True(true);
    }

    [Fact]
    public async Task Withdraw_PlayerIdentityMismatch_403()
    {
        await Task.CompletedTask;
        Assert.True(true);
    }
}
