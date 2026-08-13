using Ambiquality.Auth.Api.Domain;

namespace Ambiquality.Auth.Api.Application;

/// <summary>
/// Raised for any login failure. The message is deliberately generic to avoid
/// account enumeration.
/// </summary>
public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException()
        : base("The email address or password is incorrect.") { }
}

/// <summary>Raised when a confirmed email is required but the account is not confirmed.</summary>
public sealed class EmailNotConfirmedException : DomainException
{
    public EmailNotConfirmedException()
        : base("The email address for this account has not been confirmed.") { }
}

/// <summary>Raised when a refresh token cannot be matched to an account.</summary>
public sealed class InvalidRefreshTokenException : DomainException
{
    public InvalidRefreshTokenException()
        : base("The refresh token is invalid or no longer active.") { }
}

/// <summary>Raised when a referenced user does not exist.</summary>
public sealed class UserNotFoundException : DomainException
{
    public UserNotFoundException() : base("The requested user could not be found.") { }
}

/// <summary>
/// Raised when a chosen password fails the configured length policy. The detail
/// carries the concrete rule (e.g. the minimum length) so the caller can fix it.
/// </summary>
public sealed class WeakPasswordException : DomainException
{
    public WeakPasswordException(string message) : base(message) { }
}
