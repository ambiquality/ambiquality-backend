using Ambiquality.Auth.Api.Domain;

namespace Ambiquality.Auth.Api.Application;

/// <summary>Raised when registration targets an email that already exists.</summary>
public sealed class EmailAlreadyRegisteredException : DomainException
{
    public EmailAlreadyRegisteredException()
        : base("An account with this email address already exists.") { }
}

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
