using OroQuizClash.Domain.Authorization;

namespace OroQuizClash.Domain.Tests.Authorization;

public sealed class PermissionRoleMatrixTests
{
    [Fact]
    public void Admin_HasAllPermissions()
    {
        var admin = Role.Admin;
        Assert.Equal(14, admin.Permissions.Count);
        foreach (var perm in Permission.All)
        {
            Assert.True(admin.HasPermission(perm), $"Admin should have {perm.Name}");
        }
    }

    [Fact]
    public void GameManager_HasExpectedPermissions()
    {
        var gm = Role.GameManager;
        Assert.True(gm.HasPermission(Permission.CategoryRead));
        Assert.True(gm.HasPermission(Permission.CategoryWrite));
        Assert.True(gm.HasPermission(Permission.CategoryPublish));
        Assert.True(gm.HasPermission(Permission.QuestionRead));
        Assert.True(gm.HasPermission(Permission.QuestionWrite));
        Assert.True(gm.HasPermission(Permission.QuestionPublish));
        Assert.True(gm.HasPermission(Permission.GameCreate));
        Assert.True(gm.HasPermission(Permission.GameStart));
        Assert.True(gm.HasPermission(Permission.GamePlay));
        Assert.True(gm.HasPermission(Permission.RewardRead));
        Assert.True(gm.HasPermission(Permission.ReportRead));
        Assert.False(gm.HasPermission(Permission.RewardManage));
        Assert.False(gm.HasPermission(Permission.AuditRead));
    }

    [Fact]
    public void Player_HasExpectedPermissions()
    {
        var player = Role.Player;
        Assert.True(player.HasPermission(Permission.CategoryRead));
        Assert.True(player.HasPermission(Permission.GamePlay));
        Assert.True(player.HasPermission(Permission.RewardRead));
        Assert.True(player.HasPermission(Permission.RewardRedeem));
        Assert.False(player.HasPermission(Permission.CategoryWrite));
        Assert.False(player.HasPermission(Permission.GameCreate));
        Assert.False(player.HasPermission(Permission.RewardManage));
        Assert.False(player.HasPermission(Permission.AuditRead));
    }

    [Fact]
    public void RewardManager_HasExpectedPermissions()
    {
        var rm = Role.RewardManager;
        Assert.True(rm.HasPermission(Permission.RewardRead));
        Assert.True(rm.HasPermission(Permission.RewardManage));
        Assert.True(rm.HasPermission(Permission.ReportRead));
        Assert.True(rm.HasPermission(Permission.AuditRead));
        Assert.False(rm.HasPermission(Permission.GamePlay));
        Assert.False(rm.HasPermission(Permission.CategoryWrite));
    }

    [Fact]
    public void Permission_All_Counts14()
    {
        Assert.Equal(14, Permission.All.Count);
    }

    [Fact]
    public void Role_All_Counts4()
    {
        Assert.Equal(4, Role.All.Count);
    }
}
