using System.Security.Cryptography;
using System.Text;
using Ambiquality.Evidence.Api.Infrastructure.Security;

namespace Ambiquality.Evidence.Api.Tests.Infrastructure.Security;

public class SensorApiKeyServiceTests
{
    private readonly SensorApiKeyService _service = new();

    [Fact]
    public void Generate_PlainKey_HasExpectedPrefix()
    {
        var (plainKey, _) = _service.Generate();

        Assert.StartsWith("amq_sk_", plainKey);
    }

    [Fact]
    public void Generate_KeyHash_IsLowercaseSha256OfPlainKey()
    {
        var (plainKey, keyHash) = _service.Generate();

        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainKey)))
            .ToLowerInvariant();
        Assert.Equal(expected, keyHash);
        Assert.Equal(64, keyHash.Length);
    }

    [Fact]
    public void Generate_ProducesUniqueKeys()
    {
        var (key1, hash1) = _service.Generate();
        var (key2, hash2) = _service.Generate();

        Assert.NotEqual(key1, key2);
        Assert.NotEqual(hash1, hash2);
    }
}
