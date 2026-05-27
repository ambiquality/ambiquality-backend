using System.Security.Cryptography;
using System.Text;

namespace Ambiquality.Ingestion.Api.Infrastructure.Security;

/// <summary>
/// Hashes and verifies sensor API keys. Must match how Evidence.Api stores them:
/// lowercase-hex SHA-256 of the UTF-8 plaintext (see Evidence's SensorApiKeyService).
/// </summary>
public static class SensorKeyHasher
{
    public static string Hash(string plainKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainKey))).ToLowerInvariant();

    /// <summary>Constant-time comparison of a presented key against a stored hash.</summary>
    public static bool Verify(string presentedKey, string storedHash)
    {
        if (string.IsNullOrEmpty(presentedKey) || string.IsNullOrEmpty(storedHash))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(presentedKey)),
            Encoding.UTF8.GetBytes(storedHash));
    }
}
