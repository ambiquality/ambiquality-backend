using System.IdentityModel.Tokens.Jwt;
using Ambiquality.Evidence.Api.Application.Abstractions;

namespace Ambiquality.Evidence.Api.Infrastructure.Security;

/// <summary>
/// Runs after authentication: when the request carries a valid JWT, reads the
/// <c>sub</c> claim, lazily upserts the user's <see cref="Domain.Users.UserProjection"/>
/// and promotes the scoped <see cref="CurrentUser"/> to authenticated. Anonymous
/// requests pass through untouched so public reads still work.
/// </summary>
public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        CurrentUser currentUser,
        IUserProjectionRepository projections,
        IClock clock)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            Guid.TryParse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var authUserId))
        {
            var projectionId = await projections.FindOrCreateAsync(
                authUserId, clock.UtcNow, context.RequestAborted);
            currentUser.SetAuthenticated(authUserId, projectionId);
        }

        await next(context);
    }
}
