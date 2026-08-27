using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Games.ValueObjects;

public sealed class PlayerScore : ValueObject
{
    public int CurrentPoints { get; }
    public int SecuredPoints { get; }
    public int RoundPoints { get; }
    public int PotentialPoints { get; }
    public int TotalPoints { get; }

    public PlayerScore(
        int currentPoints = 0,
        int securedPoints = 0,
        int roundPoints = 0,
        int potentialPoints = 0,
        int totalPoints = 0)
    {
        CurrentPoints = currentPoints;
        SecuredPoints = securedPoints;
        RoundPoints = roundPoints;
        PotentialPoints = potentialPoints;
        TotalPoints = totalPoints;
    }

    public static PlayerScore Zero() => new();

    public PlayerScore Award(int amount, bool roundScoped)
    {
        if (amount <= 0)
            throw new ArgumentException("Award amount must be positive.", nameof(amount));

        return new PlayerScore(
            currentPoints: CurrentPoints + amount,
            securedPoints: roundScoped ? SecuredPoints : SecuredPoints + amount,
            roundPoints: roundScoped ? RoundPoints + amount : RoundPoints,
            potentialPoints: PotentialPoints,
            totalPoints: TotalPoints + amount);
    }

    public PlayerScore Deduct(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Deduction amount must not be negative.", nameof(amount));

        var actual = Math.Min(amount, CurrentPoints);
        var remaining = actual;

        var roundDeduction = Math.Min(remaining, RoundPoints);
        remaining -= roundDeduction;

        var securedDeduction = Math.Min(remaining, SecuredPoints);

        return new PlayerScore(
            currentPoints: CurrentPoints - actual,
            securedPoints: SecuredPoints - securedDeduction,
            roundPoints: RoundPoints - roundDeduction,
            potentialPoints: PotentialPoints,
            totalPoints: TotalPoints);
    }

    public PlayerScore Consume(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Consumption amount must not be negative.", nameof(amount));

        var actual = Math.Min(amount, CurrentPoints);
        var remaining = actual;

        var securedDeduction = Math.Min(remaining, SecuredPoints);
        remaining -= securedDeduction;

        var roundDeduction = Math.Min(remaining, RoundPoints);

        return new PlayerScore(
            currentPoints: CurrentPoints - actual,
            securedPoints: SecuredPoints - securedDeduction,
            roundPoints: RoundPoints - roundDeduction,
            potentialPoints: PotentialPoints,
            totalPoints: TotalPoints);
    }

    public PlayerScore Secure()
    {
        if (RoundPoints == 0)
            return this;

        return new PlayerScore(
            currentPoints: CurrentPoints,
            securedPoints: SecuredPoints + RoundPoints,
            roundPoints: 0,
            potentialPoints: PotentialPoints,
            totalPoints: TotalPoints);
    }

    public PlayerScore ResetRound()
    {
        return new PlayerScore(
            currentPoints: CurrentPoints,
            securedPoints: SecuredPoints,
            roundPoints: 0,
            potentialPoints: 0,
            totalPoints: TotalPoints);
    }

    public PlayerScore SetPotential(int potential)
    {
        return new PlayerScore(
            currentPoints: CurrentPoints,
            securedPoints: SecuredPoints,
            roundPoints: RoundPoints,
            potentialPoints: Math.Max(0, potential),
            totalPoints: TotalPoints);
    }

    public PlayerScore CollapseToSecured()
    {
        return new PlayerScore(
            currentPoints: SecuredPoints,
            securedPoints: SecuredPoints,
            roundPoints: 0,
            potentialPoints: PotentialPoints,
            totalPoints: TotalPoints);
    }

    public PlayerScore CollapseToZero()
    {
        return new PlayerScore(
            currentPoints: 0,
            securedPoints: 0,
            roundPoints: 0,
            potentialPoints: PotentialPoints,
            totalPoints: TotalPoints);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CurrentPoints;
        yield return SecuredPoints;
        yield return RoundPoints;
        yield return PotentialPoints;
        yield return TotalPoints;
    }
}
