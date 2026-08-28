using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Authorization;

public sealed class Permission(int id, string name) : Enumeration<Permission>(id, name)
{
    public static readonly Permission CategoryRead = new(1, "Category.Read");
    public static readonly Permission CategoryWrite = new(2, "Category.Write");
    public static readonly Permission CategoryPublish = new(3, "Category.Publish");
    public static readonly Permission QuestionRead = new(4, "Question.Read");
    public static readonly Permission QuestionWrite = new(5, "Question.Write");
    public static readonly Permission QuestionPublish = new(6, "Question.Publish");
    public static readonly Permission GameCreate = new(7, "Game.Create");
    public static readonly Permission GameStart = new(8, "Game.Start");
    public static readonly Permission GamePlay = new(9, "Game.Play");
    public static readonly Permission RewardRead = new(10, "Reward.Read");
    public static readonly Permission RewardRedeem = new(11, "Reward.Redeem");
    public static readonly Permission RewardManage = new(12, "Reward.Manage");
    public static readonly Permission ReportRead = new(13, "Report.Read");
    public static readonly Permission AuditRead = new(14, "Audit.Read");

    public static IReadOnlyList<Permission> All => [CategoryRead, CategoryWrite, CategoryPublish, QuestionRead, QuestionWrite, QuestionPublish, GameCreate, GameStart, GamePlay, RewardRead, RewardRedeem, RewardManage, ReportRead, AuditRead];
}
