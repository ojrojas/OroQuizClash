using OroQuizClash.Api.Authorization;

namespace OroQuizClash.Api.Tests.Authorization;

public sealed class RbacContractTests
{
    [Fact]
    public void SecurityPolicies_Contains14Permissions()
    {
        Assert.Equal(14, SecurityPolicies.PolicyRoles.Count);
    }

    [Fact]
    public void CategoryWrite_RequiresAdminOrGameManager()
    {
        var roles = SecurityPolicies.PolicyRoles[SecurityPolicies.CategoryWrite];
        Assert.Contains("ADMIN", roles);
        Assert.Contains("GAME_MANAGER", roles);
        Assert.DoesNotContain("PLAYER", roles);
    }

    [Fact]
    public void GamePlay_RequiresPlayerOrManagerOrAdmin()
    {
        var roles = SecurityPolicies.PolicyRoles[SecurityPolicies.GamePlay];
        Assert.Contains("PLAYER", roles);
        Assert.Contains("ADMIN", roles);
        Assert.Contains("GAME_MANAGER", roles);
    }

    [Fact]
    public void AuditRead_RequiresAdminOnly()
    {
        var roles = SecurityPolicies.PolicyRoles[SecurityPolicies.AuditRead];
        Assert.Contains("ADMIN", roles);
        Assert.DoesNotContain("PLAYER", roles);
        Assert.Single(roles);
    }

    [Fact]
    public void RewardManage_RequiresAdminOrRewardManager()
    {
        var roles = SecurityPolicies.PolicyRoles[SecurityPolicies.RewardManage];
        Assert.Contains("ADMIN", roles);
        Assert.Contains("REWARD_MANAGER", roles);
        Assert.DoesNotContain("PLAYER", roles);
    }
}
