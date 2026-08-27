using BuildingBlocks.Kernel.Domain.Entities;

namespace OroQuizClash.Domain.Rewards;

public sealed class RedemptionTransition : Entity<RedemptionTransitionId>
{
    public RedemptionStatus Status { get; private set; } = null!;
    public Guid ActorId { get; private set; }
    public DateTimeOffset At { get; private set; }

    private RedemptionTransition() { }

    internal RedemptionTransition(RedemptionStatus status, Guid actorId)
        : base(RedemptionTransitionId.New())
    {
        Status = status;
        ActorId = actorId;
        At = DateTimeOffset.UtcNow;
    }
}
