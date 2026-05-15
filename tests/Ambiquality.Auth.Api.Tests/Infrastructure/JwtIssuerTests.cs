using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Domain.Users;
using Ambiquality.Auth.Api.Infrastructure.Security;
using Microsoft.IdentityModel.Tokens;

namespace Ambiquality.Auth.Api.Tests.Infrastructure;

public class JwtIssuerTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    private static readonly JwtOptions Options = new()
    {
        Issuer = "ambiquality-auth",
        Audience = "ambiquality",
        Secret = "super-secret-signing-key-at-least-32-bytes-long",
        AccessTokenMinutes = 15
    };

    private static User CreateConfirmedUser()
    {
        var user = User.Register(
            Email.Create("user@example.com"), "hash", "confirm-hash",
            Now.AddDays(-1), TimeSpan.FromHours(24));
        user.ConfirmEmail("confirm-hash", Now.AddDays(-1));
        return user;
    }

    private static JwtIssuer CreateIssuer(IClock clock) => new(Options, clock);

    [Fact]
    public void Issue_ProducesTokenWithExpectedClaims()
    {
        var user = CreateConfirmedUser();
        var issuer = CreateIssuer(new TestSupport.FakeClock(Now));

        var access = issuer.Issue(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(access.Value);

        Assert.Equal("ambiquality-auth", jwt.Issuer);
        Assert.Contains("ambiquality", jwt.Audiences);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Email
            && c.Value == "user@example.com");
    }

    [Fact]
    public void Issue_SetsExpiryFromClockPlusConfiguredLifetime()
    {
        var user = CreateConfirmedUser();
        var issuer = CreateIssuer(new TestSupport.FakeClock(Now));

        var access = issuer.Issue(user);

        Assert.Equal(Now.AddMinutes(15), access.ExpiresAt);
    }

    [Fact]
    public void Issue_ProducesTokenThatValidatesAgainstSigningKey()
    {
        var user = CreateConfirmedUser();
        var issuer = CreateIssuer(new TestSupport.FakeClock(Now));
        var access = issuer.Issue(user);

        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.Secret)),
            ValidateLifetime = false,
            ClockSkew = TimeSpan.Zero
        };

        var principal = new JwtSecurityTokenHandler()
            .ValidateToken(access.Value, validationParameters, out _);

        Assert.Equal(user.Id.ToString(),
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public void Issue_TokenSignedWithWrongKey_FailsValidation()
    {
        var user = CreateConfirmedUser();
        var issuer = CreateIssuer(new TestSupport.FakeClock(Now));
        var access = issuer.Issue(user);

        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("a-completely-different-signing-key-of-32b")),
            ValidateLifetime = false
        };

        Assert.ThrowsAny<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(
                access.Value, validationParameters, out _));
    }
}
