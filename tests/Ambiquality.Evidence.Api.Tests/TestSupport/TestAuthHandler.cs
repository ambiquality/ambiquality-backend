using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ambiquality.Evidence.Api.Tests.TestSupport;

/// <summary>
/// Test authentication scheme standing in for real JWT bearer validation. By
/// default it authenticates every request as <see cref="DefaultSub"/>, so the
/// existing endpoint tests keep working. Tests can act as a different user by
/// sending the <see cref="SubHeader"/> header, or force an anonymous request
/// (e.g. to assert a 401 or unauthenticated read) with the
/// <see cref="AnonymousHeader"/> header.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string SubHeader = "X-Test-Sub";
    public const string AnonymousHeader = "X-Test-Anonymous";

    public static readonly Guid DefaultSub = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey(AnonymousHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var sub = Request.Headers.TryGetValue(SubHeader, out var raw) && Guid.TryParse(raw, out var parsed)
            ? parsed
            : DefaultSub;

        var identity = new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, sub.ToString())],
            SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
