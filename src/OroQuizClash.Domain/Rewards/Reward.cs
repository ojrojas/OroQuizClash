using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.Results;

using OroQuizClash.Domain.Rewards.Events;
using OroQuizClash.Domain.Rewards.Rules;

namespace OroQuizClash.Domain.Rewards;

public sealed class Reward : AggregateRoot<RewardId>
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int PointsRequired { get; private set; }
    public int Stock { get; private set; }
    public RewardStatus Status { get; private set; } = null!;
    public DateTimeOffset? ExpirationDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private Reward() { }

    public static Result<Reward> Create(
        string name,
        string description,
        int pointsRequired,
        int stock,
        DateTimeOffset? expirationDate = null)
    {
        var nameRule = new RewardNameValidRule(name);
        if (nameRule.IsBroken()) return Result.Failure<Reward>(RewardErrors.InvalidRewardName);

        var pointsRule = new PointsRequiredPositiveRule(pointsRequired);
        if (pointsRule.IsBroken()) return Result.Failure<Reward>(RewardErrors.InvalidPointsRequired);

        var stockRule = new StockNotNegativeRule(stock);
        if (stockRule.IsBroken()) return Result.Failure<Reward>(RewardErrors.InvalidStock);

        var reward = new Reward
        {
            Id = RewardId.New(),
            Name = name.Trim(),
            Description = description.Trim(),
            PointsRequired = pointsRequired,
            Stock = stock,
            Status = RewardStatus.Active,
            ExpirationDate = expirationDate,
            CreatedAt = DateTimeOffset.UtcNow
        };

        reward.RaiseDomainEvent(new RewardCreatedDomainEvent(reward.Id.Value));
        return Result.Success(reward);
    }

    public Result Update(string? name = null, string? description = null, int? pointsRequired = null, int? stock = null, DateTimeOffset? expirationDate = null)
    {
        if (name is not null)
        {
            var rule = new RewardNameValidRule(name);
            if (rule.IsBroken()) return Result.Failure(RewardErrors.InvalidRewardName);
            Name = name.Trim();
        }

        if (description is not null)
            Description = description.Trim();

        if (pointsRequired is not null)
        {
            var rule = new PointsRequiredPositiveRule(pointsRequired.Value);
            if (rule.IsBroken()) return Result.Failure(RewardErrors.InvalidPointsRequired);
            PointsRequired = pointsRequired.Value;
        }

        if (stock is not null)
        {
            var rule = new StockNotNegativeRule(stock.Value);
            if (rule.IsBroken()) return Result.Failure(RewardErrors.InvalidStock);
            Stock = stock.Value;
        }

        if (expirationDate is not null)
            ExpirationDate = expirationDate;

        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new RewardUpdatedDomainEvent(Id.Value));
        return Result.Success();
    }

    public Result Activate()
    {
        if (Status.IsActive) return Result.Failure(RewardErrors.RewardAlreadyActive);

        Status = RewardStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new RewardStatusChangedDomainEvent(Id.Value, Status.Name));
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (Status.IsInactive) return Result.Failure(RewardErrors.RewardAlreadyInactive);

        Status = RewardStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new RewardStatusChangedDomainEvent(Id.Value, Status.Name));
        return Result.Success();
    }

    public Result ReserveStock(DateTimeOffset now)
    {
        var rule = new RewardAvailableRule(Status, Stock, ExpirationDate, now);
        if (rule.IsBroken()) return Result.Failure(RewardErrors.RewardUnavailable);

        Stock--;
        return Result.Success();
    }

    public void ReleaseStock()
    {
        Stock++;
    }

    public bool IsAvailable(DateTimeOffset now) =>
        Status.IsActive &&
        Stock > 0 &&
        (!ExpirationDate.HasValue || ExpirationDate.Value > now);
}
