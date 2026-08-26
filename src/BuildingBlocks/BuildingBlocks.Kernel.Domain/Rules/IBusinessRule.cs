namespace BuildingBlocks.Kernel.Domain.Rules;

/// <summary>
/// An invariant of the domain that must hold before a state change is applied.
/// </summary>
public interface IBusinessRule
{
    bool IsBroken();

    string Message { get; }
}
