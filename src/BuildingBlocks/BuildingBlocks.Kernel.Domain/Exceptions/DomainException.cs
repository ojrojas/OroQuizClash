namespace BuildingBlocks.Kernel.Domain.Exceptions;

/// <summary>
/// Base exception for all errors originating in the domain layer.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}