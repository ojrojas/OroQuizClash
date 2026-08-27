using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.Results;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Rewards.Events;
using OroQuizClash.Domain.Rewards.Rules;

namespace OroQuizClash.Domain.Rewards;

public sealed class RewardRedemption : AggregateRoot<RewardRedemptionId>
{
    private readonly List<RedemptionTransition> _transitions = [];

    public Guid PlayerId { get; private set; }
    public RewardId RewardId { get; private set; } = null!;
    public GameId GameId { get; private set; } = null!;
    public int Points { get; private set; }
    public RedemptionStatus Status { get; private set; } = null!;
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public Guid? IdempotencyKey { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<RedemptionTransition> Transitions => _transitions.AsReadOnly();

    private RewardRedemption() { }

    public static Result<RewardRedemption> Create(
        Guid playerId,
        RewardId rewardId,
        GameId gameId,
        int points,
        Guid? idempotencyKey = null)
    {
        if (points <= 0)
            return Result.Failure<RewardRedemption>(RewardErrors.InvalidPointsRequired);

        var redemption = new RewardRedemption
        {
            Id = RewardRedemptionId.New(),
            PlayerId = playerId,
            RewardId = rewardId,
            GameId = gameId,
            Points = points,
            Status = RedemptionStatus.Requested,
            RequestedAt = DateTimeOffset.UtcNow,
            IdempotencyKey = idempotencyKey
        };

        redemption._transitions.Add(new RedemptionTransition(RedemptionStatus.Requested, playerId));
        redemption.RaiseDomainEvent(new RewardRedeemedDomainEvent(
            redemption.Id.Value, rewardId.Value, playerId, gameId.Value, points));

        return Result.Success(redemption);
    }

    public static Result<RewardRedemption> CreateAsConsolation(
        Guid playerId,
        Guid rewardId,
        Guid gameId)
    {
        if (playerId == Guid.Empty)
            return Result.Failure<RewardRedemption>(RewardErrors.InvalidPointsRequired);

        if (rewardId == Guid.Empty)
            return Result.Failure<RewardRedemption>(RewardErrors.InvalidPointsRequired);

        if (gameId == Guid.Empty)
            return Result.Failure<RewardRedemption>(RewardErrors.InvalidPointsRequired);

        var redemption = new RewardRedemption
        {
            Id = RewardRedemptionId.New(),
            PlayerId = playerId,
            RewardId = new RewardId(rewardId),
            GameId = new GameId(gameId),
            Points = 0,
            Status = RedemptionStatus.Approved,
            RequestedAt = DateTimeOffset.UtcNow,
            DeliveredAt = DateTimeOffset.UtcNow
        };

        var systemActor = Guid.Empty;
        redemption._transitions.Add(new RedemptionTransition(RedemptionStatus.Approved, systemActor));
        redemption.RaiseDomainEvent(new RewardRedeemedDomainEvent(
            redemption.Id.Value, rewardId, playerId, gameId, 0));

        return Result.Success(redemption);
    }

    public Result Approve(Guid managerId)
    {
        var rule = new RedemptionTransitionRule(Status, RedemptionStatus.Approved);
        if (rule.IsBroken()) return Result.Failure(RewardErrors.InvalidRedemptionTransition);

        Status = RedemptionStatus.Approved;
        AppendTransition(RedemptionStatus.Approved, managerId);
        return Result.Success();
    }

    public Result Reject(Guid managerId)
    {
        var rule = new RedemptionTransitionRule(Status, RedemptionStatus.Rejected);
        if (rule.IsBroken()) return Result.Failure(RewardErrors.InvalidRedemptionTransition);

        Status = RedemptionStatus.Rejected;
        AppendTransition(RedemptionStatus.Rejected, managerId);
        return Result.Success();
    }

    public Result Deliver(Guid managerId)
    {
        var rule = new RedemptionTransitionRule(Status, RedemptionStatus.Delivered);
        if (rule.IsBroken()) return Result.Failure(RewardErrors.InvalidRedemptionTransition);

        Status = RedemptionStatus.Delivered;
        DeliveredAt = DateTimeOffset.UtcNow;
        AppendTransition(RedemptionStatus.Delivered, managerId);
        return Result.Success();
    }

    public Result Cancel(Guid playerId)
    {
        if (playerId != PlayerId)
            return Result.Failure(RewardErrors.NotRedemptionOwner);

        var rule = new RedemptionTransitionRule(Status, RedemptionStatus.Cancelled);
        if (rule.IsBroken()) return Result.Failure(RewardErrors.InvalidRedemptionTransition);

        Status = RedemptionStatus.Cancelled;
        AppendTransition(RedemptionStatus.Cancelled, playerId);
        return Result.Success();
    }

    private void AppendTransition(RedemptionStatus status, Guid actorId)
    {
        _transitions.Add(new RedemptionTransition(status, actorId));
        RaiseDomainEvent(new RedemptionStatusChangedDomainEvent(Id.Value, status.Name, actorId));
    }
}
