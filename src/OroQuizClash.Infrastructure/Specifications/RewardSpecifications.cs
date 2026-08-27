using BuildingBlocks.Kernel.Domain.Specifications;

using OroQuizClash.Domain.Rewards;

namespace OroQuizClash.Infrastructure.Specifications;

public sealed class AvailableRewardsSpecification : Specification<Reward>
{
    public AvailableRewardsSpecification(DateTimeOffset now)
    {
        Where(r => r.Status == RewardStatus.Active &&
                   r.Stock > 0 &&
                   (!r.ExpirationDate.HasValue || r.ExpirationDate.Value > now));
        ApplyOrderBy(r => r.PointsRequired);
    }
}

public sealed class RewardByIdSpecification : Specification<Reward>
{
    public RewardByIdSpecification(RewardId id)
    {
        Where(r => r.Id == id);
    }

    public RewardByIdSpecification(Guid id) : this(new RewardId(id)) { }
}

public sealed class AllRewardsSpecification : Specification<Reward>
{
    public AllRewardsSpecification()
    {
        Where(_ => true);
    }
}
