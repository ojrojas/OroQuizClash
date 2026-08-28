using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Audit;

public sealed class AuditAction(int id, string name) : Enumeration<AuditAction>(id, name)
{
    public static readonly AuditAction GameCreated = new(1, "GameCreated");
    public static readonly AuditAction GameConfigured = new(2, "GameConfigured");
    public static readonly AuditAction GameStarted = new(3, "GameStarted");
    public static readonly AuditAction PlayerJoined = new(4, "PlayerJoined");
    public static readonly AuditAction RoundStarted = new(5, "RoundStarted");
    public static readonly AuditAction QuestionPresented = new(6, "QuestionPresented");
    public static readonly AuditAction AnswerSubmitted = new(7, "AnswerSubmitted");
    public static readonly AuditAction AnswerEvaluated = new(8, "AnswerEvaluated");
    public static readonly AuditAction PointsAwarded = new(9, "PointsAwarded");
    public static readonly AuditAction PointsRemoved = new(10, "PointsRemoved");
    public static readonly AuditAction PlayerWithdrawn = new(11, "PlayerWithdrawn");
    public static readonly AuditAction PlayerEliminated = new(12, "PlayerEliminated");
    public static readonly AuditAction GameFinished = new(13, "GameFinished");
    public static readonly AuditAction RewardRedeemed = new(14, "RewardRedeemed");
    public static readonly AuditAction ConsolationGranted = new(15, "ConsolationGranted");
    public static readonly AuditAction AdministrativeAdjustment = new(16, "AdministrativeAdjustment");

    public static IReadOnlyList<AuditAction> All =>
    [
        GameCreated, GameConfigured, GameStarted, PlayerJoined, RoundStarted, QuestionPresented,
        AnswerSubmitted, AnswerEvaluated, PointsAwarded, PointsRemoved,
        PlayerWithdrawn, PlayerEliminated, GameFinished, RewardRedeemed, ConsolationGranted, AdministrativeAdjustment
    ];
}
