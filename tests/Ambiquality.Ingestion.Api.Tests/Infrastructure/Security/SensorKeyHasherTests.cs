using System.Security.Cryptography;
using System.Text;
using Ambiquality.Ingestion.Api.Infrastructure.Security;

namespace Ambiquality.Ingestion.Api.Tests.Infrastructure.Security;

public class SensorKeyHasherTests
{
    [Fact]
    public void Hash_IsLowercaseHexSha256()
    {
        const string key = "amq_sk_example";

        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))
            .ToLowerInvariant();

        Assert.Equal(expected, SensorKeyHasher.Hash(key));
    }

    [Fact]
    public void Verify_MatchingKey_ReturnsTrue()
    {
        const string key = "amq_sk_correct";
        var hash = SensorKeyHasher.Hash(key);

        Assert.True(SensorKeyHasher.Verify(key, hash));
    }

    [Fact]
    public void Verify_WrongKey_ReturnsFalse()
    {
        var hash = SensorKeyHasher.Hash("amq_sk_correct");

        Assert.False(SensorKeyHasher.Verify("amq_sk_wrong", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Verify_EmptyPresentedKey_ReturnsFalse(string? presented)
    {
        var hash = SensorKeyHasher.Hash("amq_sk_correct");

        Assert.False(SensorKeyHasher.Verify(presented!, hash));
    }
}
