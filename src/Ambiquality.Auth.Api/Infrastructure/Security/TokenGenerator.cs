using System.Security.Cryptography;
using System.Text;
using Ambiquality.Auth.Api.Application.Abstractions;

namespace Ambiquality.Auth.Api.Infrastructure.Security;

/// <summary>
/// Generates 32-byte crypto-random tokens. The raw token is URL-safe base64;
/// the stored hash is the lowercase hex SHA-256 of the raw token.
/// </summary>
public sealed class TokenGenerator : ITokenGenerator
{
    private const int TokenSizeBytes = 32;

    public GeneratedToken Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenSizeBytes);
        var rawToken = Base64UrlEncode(bytes);
        return new GeneratedToken(rawToken, Hash(rawToken));
    }

    public string Hash(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
