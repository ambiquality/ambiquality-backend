using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Ambiquality.Auth.Api.Api.Contracts;
using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Application.Users;
using Ambiquality.Auth.Api.Domain;
using Ambiquality.Auth.Api.Domain.Users;
using Microsoft.AspNetCore.Mvc;

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
        })
        .WithName("GetMe")
        .WithSummary("Get the authenticated user's profile")
        .WithDescription("Returns the ID, email, and email-confirmation status of the user identified by the Bearer JWT.")
        .Produces<MeResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

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
        })
        .WithName("ChangePassword")
        .WithSummary("Change the authenticated user's password")
        .WithDescription(
            "Verifies the current password then replaces it with the new password. " +
            "All existing refresh tokens remain valid after the change.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

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
                    new ChangeEmailCommand(userId, request.CurrentPassword, request.NewEmail), cancellationToken);
                return Results.Accepted();
            }
            catch (DomainException ex)
            {
                return Problems.ToResult(ex);
            }
        })
        .WithName("ChangeEmail")
        .WithSummary("Request an email address change")
        .WithDescription(
            "Validates the current password and sends a confirmation link to the new email address. " +
            "The email is not actually changed until the link is followed (GET /account/confirm-email-change).")
        .Produces(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async (
            HttpContext httpContext,
            AuthOptions options,
            ClaimsPrincipal principal,
            LogoutHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            await handler.HandleAsync(new LogoutCommand(userId), cancellationToken);
            RefreshTokenCookie.Clear(httpContext.Response, options);
            return Results.NoContent();
        })
        .WithName("Logout")
        .WithSummary("Invalidate the current user's refresh tokens")
        .WithDescription(
            "Revokes all active refresh tokens for the authenticated user. " +
            "Existing access tokens remain valid until they expire naturally.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapDelete("/{id:guid}", async (
            [Description("The ID of the account to delete. Must equal the authenticated user's own ID.")] Guid id,
            // DELETE does not infer a body parameter; bind it explicitly.
            [FromBody] DeleteAccountRequest request,
            ClaimsPrincipal principal,
            DeleteAccountHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            // A user may only delete their own account; the acting identity comes
            // from the JWT, never from the route, so a mismatch is forbidden.
            if (userId != id)
                return Results.Problem(
                    detail: "You can only delete your own account.",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden",
                    type: "urn:ambiquality:auth:forbidden");

            try
            {
                await handler.HandleAsync(
                    new DeleteAccountCommand(id, request.CurrentPassword), cancellationToken);
                return Results.NoContent();
            }
            catch (DomainException ex)
            {
                return Problems.ToResult(ex);
            }
        })
        .WithName("DeleteAccount")
        .WithSummary("Permanently delete the authenticated user's account")
        .WithDescription(
            "Verifies the supplied current password, then permanently deletes the account and all its " +
            "refresh and verification tokens. The path ID must match the authenticated user's own ID; " +
            "deleting another user's account is forbidden. This action is irreversible.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/confirm-email-change", async (
            [Description("The single-use token from the email-change confirmation link.")] string token,
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
        })
        .WithName("ConfirmEmailChange")
        .WithSummary("Confirm a pending email address change")
        .WithDescription(
            "Finalises the email change initiated by POST /account/change-email. " +
            "The token is a single-use value from the confirmation link sent to the new address.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

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
