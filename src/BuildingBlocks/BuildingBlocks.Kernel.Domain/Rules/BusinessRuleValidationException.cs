using BuildingBlocks.Kernel.Domain.Exceptions;

namespace BuildingBlocks.Kernel.Domain.Rules;

/// <summary>
/// Thrown when a <see cref="IBusinessRule"/> is broken.
/// </summary>
public sealed class BusinessRuleValidationException(IBusinessRule brokenRule)
    : DomainException(brokenRule.Message)
{
    public IBusinessRule BrokenRule { get; } = brokenRule;
}