using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Domain.Users;
using Microsoft.IdentityModel.Tokens;

namespace Ambiquality.Auth.Api.Infrastructure.Security;

/// <summary>
/// Issues short-lived HMAC-SHA256 access tokens. Claims: <c>sub</c> (user GUID),
/// <c>email</c>, <c>jti</c>, plus issuer / audience / expiry.
/// </summary>
public sealed class JwtIssuer(JwtOptions options, IClock clock) : IJwtIssuer
{
    private readonly JwtSecurityTokenHandler _handler = new();

    public AccessToken Issue(User user)
    {
        var issuedAt = clock.UtcNow;
        var expiresAt = issuedAt.AddMinutes(options.AccessTokenMinutes);

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email.Value),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AccessToken(_handler.WriteToken(token), expiresAt);
    }
}
