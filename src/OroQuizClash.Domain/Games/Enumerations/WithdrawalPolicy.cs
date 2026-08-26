using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Games.Enumerations;

public sealed class WithdrawalPolicy(int id, string name) : Enumeration<WithdrawalPolicy>(id, name)
{
    public static readonly WithdrawalPolicy LoseAll = new(1, "LOSE_ALL");
    public static readonly WithdrawalPolicy KeepCurrentScore = new(2, "KEEP_CURRENT_SCORE");
    public static readonly WithdrawalPolicy KeepSecuredScore = new(3, "KEEP_SECURED_SCORE");
    public static readonly WithdrawalPolicy KeepCheckpointScore = new(4, "KEEP_CHECKPOINT_SCORE");
}