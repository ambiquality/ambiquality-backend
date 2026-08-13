using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Domain;
using Ambiquality.Auth.Api.Domain.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ambiquality.Auth.Api.Api;

/// <summary>
/// A status code, stable type URI, title and detail describing a domain error
/// as an RFC 9457 problem.
/// </summary>
public sealed record ProblemDescriptor(int StatusCode, string Type, string Title, string Detail);

/// <summary>
/// Maps domain / application exceptions to RFC 9457 Problem Details. Type URIs
/// are stable URNs so clients can branch on them; auth-failure detail messages
/// are deliberately generic to avoid account enumeration.
/// </summary>
public static class Problems
{
    private const string TypePrefix = "urn:ambiquality:auth:";

    public static ProblemDescriptor Describe(DomainException exception) => exception switch
    {
        InvalidEmailException => new ProblemDescriptor(
            StatusCodes.Status400BadRequest,
            TypePrefix + "invalid-email",
            "Invalid email address",
            exception.Message),

        InvalidCredentialsException => new ProblemDescriptor(
            StatusCodes.Status401Unauthorized,
            TypePrefix + "invalid-credentials",
            "Invalid credentials",
            exception.Message),

        EmailNotConfirmedException => new ProblemDescriptor(
            StatusCodes.Status401Unauthorized,
            TypePrefix + "email-not-confirmed",
            "Email not confirmed",
            exception.Message),

        InvalidRefreshTokenException => new ProblemDescriptor(
            StatusCodes.Status401Unauthorized,
            TypePrefix + "invalid-refresh-token",
            "Invalid refresh token",
            exception.Message),

        UserNotFoundException => new ProblemDescriptor(
            StatusCodes.Status404NotFound,
            TypePrefix + "user-not-found",
            "User not found",
            exception.Message),

        _ => new ProblemDescriptor(
            StatusCodes.Status400BadRequest,
            TypePrefix + "domain-rule-violation",
            "Domain rule violation",
            exception.Message)
    };

    /// <summary>Converts a domain exception into an RFC 9457 problem result.</summary>
    public static ProblemHttpResult ToResult(DomainException exception)
    {
        var descriptor = Describe(exception);
        return TypedResults.Problem(
            detail: descriptor.Detail,
            statusCode: descriptor.StatusCode,
            title: descriptor.Title,
            type: descriptor.Type);
    }
}
