namespace Ambiquality.Auth.Api.Domain;

/// <summary>
/// Base type for all domain rule violations. The API layer maps these to
/// RFC 9457 Problem Details responses.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
