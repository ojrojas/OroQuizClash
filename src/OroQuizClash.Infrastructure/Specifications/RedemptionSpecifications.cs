
using BuildingBlocks.Kernel.Domain.Specifications;

using OroQuizClash.Domain.Rewards;

namespace OroQuizClash.Infrastructure.Specifications;

public sealed class RedemptionByIdSpecification : Specification<RewardRedemption>
{
    public RedemptionByIdSpecification(RewardRedemptionId id)
    {
        Where(r => r.Id == id);
    }

    public RedemptionByIdSpecification(Guid id) : this(new RewardRedemptionId(id)) { }
}

public sealed class RedemptionsByPlayerSpecification : Specification<RewardRedemption>
{
    public RedemptionsByPlayerSpecification(Guid playerId)
    {
        Where(r => r.PlayerId == playerId);
        ApplyOrderByDescending(r => r.RequestedAt);
    }
}

public sealed class RedemptionsByStatusSpecification : Specification<RewardRedemption>
{
    public RedemptionsByStatusSpecification(RedemptionStatus? status = null)
    {
        if (status is not null)
            Where(r => r.Status == status);

        ApplyOrderByDescending(r => r.RequestedAt);
    }
}

public sealed class RedemptionByIdempotencyKeySpecification : Specification<RewardRedemption>
{
    public RedemptionByIdempotencyKeySpecification(Guid playerId, Guid idempotencyKey)
    {
        Where(r => r.PlayerId == playerId && r.IdempotencyKey == idempotencyKey);
    }
}
