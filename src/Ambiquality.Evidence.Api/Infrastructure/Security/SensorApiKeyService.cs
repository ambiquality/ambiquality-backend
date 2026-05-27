using System.Security.Cryptography;
using System.Text;
using Ambiquality.Evidence.Api.Application.Abstractions;

namespace Ambiquality.Evidence.Api.Infrastructure.Security;

/// <inheritdoc />
public sealed class SensorApiKeyService : ISensorApiKeyService
{
    // Distinguishes Ambiquality sensor keys in logs/secret scanners (cf. GitHub's ghp_, Stripe's sk_).
    private const string Prefix = "amq_sk_";
    private const int KeyByteLength = 32;

    public (string PlainKey, string KeyHash) Generate()
    {
        var plainKey = Prefix + Base64Url(RandomNumberGenerator.GetBytes(KeyByteLength));
        return (plainKey, Hash(plainKey));
    }

    private static string Hash(string plainKey)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(plainKey));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
