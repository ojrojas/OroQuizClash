using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Authorization;

public sealed class Role(int id, string name) : Enumeration<Role>(id, name)
{
    public static readonly Role Admin = new(1, "ADMIN");
    public static readonly Role GameManager = new(2, "GAME_MANAGER");
    public static readonly Role Player = new(3, "PLAYER");
    public static readonly Role RewardManager = new(4, "REWARD_MANAGER");

    public IReadOnlyList<Permission> Permissions => this switch
    {
        _ when this == Admin => Permission.All,
        _ when this == GameManager => [Permission.CategoryRead, Permission.CategoryWrite, Permission.CategoryPublish, Permission.QuestionRead, Permission.QuestionWrite, Permission.QuestionPublish, Permission.GameCreate, Permission.GameStart, Permission.GamePlay, Permission.RewardRead, Permission.ReportRead],
        _ when this == Player => [Permission.CategoryRead, Permission.GamePlay, Permission.RewardRead, Permission.RewardRedeem],
        _ when this == RewardManager => [Permission.RewardRead, Permission.RewardManage, Permission.ReportRead, Permission.AuditRead],
        _ => []
    };

    public bool HasPermission(Permission permission) => Permissions.Contains(permission);

    public static IReadOnlyList<Role> All => [Admin, GameManager, Player, RewardManager];
}
