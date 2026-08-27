using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Rewards;

public sealed class RedemptionStatus(int id, string name) : Enumeration<RedemptionStatus>(id, name)
{
    public static readonly RedemptionStatus Requested = new(1, "REQUESTED");
    public static readonly RedemptionStatus Approved = new(2, "APPROVED");
    public static readonly RedemptionStatus Rejected = new(3, "REJECTED");
    public static readonly RedemptionStatus Delivered = new(4, "DELIVERED");
    public static readonly RedemptionStatus Cancelled = new(5, "CANCELLED");

    public bool IsTerminal =>
        this == Rejected ||
        this == Delivered ||
        this == Cancelled;
}
