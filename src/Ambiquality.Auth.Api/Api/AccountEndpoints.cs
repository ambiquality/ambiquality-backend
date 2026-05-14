using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Ambiquality.Auth.Api.Api.Contracts;
using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Application.Users;
using Ambiquality.Auth.Api.Domain;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Api;

/// <summary>
/// Authenticated account-management endpoints. The acting user is taken from
/// the JWT <c>sub</c> claim, never from the request body.
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/account")
            .WithTags("Account")
            .RequireAuthorization();

        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            IUserRepository repository,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var user = await repository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return Problems.ToResult(new UserNotFoundException());

            return Results.Ok(new MeResponse(user.Id, user.Email.Value, user.EmailConfirmed));
        });

        group.MapPost("/change-password", async (
            ChangePasswordRequest request,
            ClaimsPrincipal principal,
            ChangePasswordHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                await handler.HandleAsync(
                    new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword),
                    cancellationToken);
                return Results.Ok();
            }
            catch (DomainException ex)
            {
                return Problems.ToResult(ex);
            }
        });

        group.MapPost("/change-email", async (
            ChangeEmailRequest request,
            ClaimsPrincipal principal,
            ChangeEmailHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                await handler.HandleAsync(
                    new ChangeEmailCommand(userId, request.NewEmail), cancellationToken);
                return Results.Accepted();
            }
            catch (DomainException ex)
            {
                return Problems.ToResult(ex);
            }
        });

        group.MapGet("/confirm-email-change", async (
            string token,
            ClaimsPrincipal principal,
            ConfirmEmailChangeHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                await handler.HandleAsync(
                    new ConfirmEmailChangeCommand(userId, token), cancellationToken);
                return Results.Ok();
            }
            catch (DomainException ex)
            {
                return Problems.ToResult(ex);
            }
        });

        return app;
    }

    /// <summary>Extracts the user GUID from the <c>sub</c> (or NameIdentifier) claim.</summary>
    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }
}
