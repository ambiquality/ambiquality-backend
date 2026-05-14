namespace Ambiquality.Auth.Api.Domain.Users;

/// <summary>Raised when an email address fails value-object validation.</summary>
public sealed class InvalidEmailException : DomainException
{
    public InvalidEmailException(string message) : base(message) { }
}
