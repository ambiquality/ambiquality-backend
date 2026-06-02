using System.ComponentModel;
using Ambiquality.Auth.Api.Api.Contracts;
using Ambiquality.Auth.Api.Application.Users;
using Ambiquality.Auth.Api.Domain;

namespace Ambiquality.Auth.Api.Api;

/// <summary>
/// Anonymous authentication endpoints. Every domain failure is translated into
/// an RFC 9457 <c>application/problem+json</c> response by <see cref="Problems"/>.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/").WithTags("Authentication");

        group.MapPost("/register", async (
            RegisterRequest request,
            RegisterUserHandler handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await handler.HandleAsync(
                    new RegisterUserCommand(request.Email, request.Password), cancellationToken);
                return Results.Created((string?)null, null);
            }
            catch (DomainException ex)
            {
                return Problems.ToResult(ex);
            }
        })
        .WithName("RegisterUser")
        .WithSummary("Register a new user account")
        .WithDescription(
            "Creates a new user with the provided email and password. " +
            "A confirmation email is sent; the account cannot be used until the email is confirmed via GET /confirm-email.")
        .Produces(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", async (
            LoginRequest request,
            LoginHandler handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await handler.HandleAsync(
                    new LoginCommand(request.Email, request.Password), cancellationToken);
                return Results.Ok(new AuthResponse(
                    result.AccessToken,
                    result.AccessTokenExpiresAt,
                    result.RefreshToken,
                    result.RefreshTokenExpiresAt));
            }
            catch (DomainException ex)
            {
                return Problems.ToResult(ex);
            }
        })
        .WithName("Login")
        .WithSummary("Log in and obtain JWT tokens")
        .WithDescription(
            "Validates the credentials and returns a short-lived access token plus a long-lived refresh token. " +
            "The account email must be confirmed before login succeeds.")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .RequireRateLimiting("login");

        group.MapPost("/refresh", async (
            RefreshRequest request,
            RefreshTokenHandler handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await handler.HandleAsync(
                    new RefreshTokenCommand(request.RefreshToken), cancellationToken);
                return Results.Ok(new AuthResponse(
                    result.AccessToken,
                    result.AccessTokenExpiresAt,
                    result.RefreshToken,
                    result.RefreshTokenExpiresAt));
            }
            catch (DomainException ex)
            {
                return Problems.ToResult(ex);
            }
        })
        .WithName("RefreshToken")
        .WithSummary("Exchange a refresh token for a new token pair")
        .WithDescription(
            "Issues a new access token and refresh token in exchange for a valid, non-expired refresh token. " +
            "The old refresh token is invalidated on success.")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/confirm-email", async (
            [Description("The GUID of the user to confirm.")] Guid userId,
            [Description("The single-use verification token sent via email.")] string token,
            ConfirmEmailHandler handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await handler.HandleAsync(
                    new ConfirmEmailCommand(userId, token), cancellationToken);
                return Results.Ok();
            }
            catch (DomainException ex)
            {
                return Problems.ToResult(ex);
            }
        })
        .WithName("ConfirmEmail")
        .WithSummary("Confirm a user's email address")
        .WithDescription(
            "Verifies the one-time token that was emailed after registration. " +
            "Both userId and token query parameters are required and are included in the confirmation link sent by the server.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/resend-confirmation", async (
            ResendConfirmationRequest request,
            ResendConfirmationHandler handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await handler.HandleAsync(
                    new ResendConfirmationCommand(request.Email), cancellationToken);
                // Always 202 — never reveal whether the account exists.
                return Results.Accepted();
            }
            catch (DomainException ex)
            {
                return Problems.ToResult(ex);
            }
        })
        .WithName("ResendConfirmation")
        .WithSummary("Re-send the email confirmation link")
        .WithDescription(
            "Triggers a new confirmation email for the given address. " +
            "Always returns 202 regardless of whether the address is registered, to prevent account enumeration.")
        .Produces(StatusCodes.Status202Accepted);

        return app;
    }
}
