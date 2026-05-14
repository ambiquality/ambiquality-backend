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
        });

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
        });

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
        });

        group.MapGet("/confirm-email", async (
            Guid userId,
            string token,
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
        });

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
        });

        return app;
    }
}
