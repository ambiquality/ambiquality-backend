using System.Security.Cryptography;
using System.Text;
using Ambiquality.Auth.Api.Infrastructure.Security;

namespace Ambiquality.Auth.Api.Tests.Infrastructure;

public class TokenGeneratorTests
{
    private readonly TokenGenerator _generator = new();

    [Fact]
    public void Generate_ProducesNonEmptyRawTokenAndHash()
    {
        var token = _generator.Generate();

        Assert.False(string.IsNullOrWhiteSpace(token.RawToken));
        Assert.False(string.IsNullOrWhiteSpace(token.TokenHash));
    }

    [Fact]
    public void Generate_ProducesUniqueRawTokens()
    {
        var first = _generator.Generate();
        var second = _generator.Generate();

        Assert.NotEqual(first.RawToken, second.RawToken);
    }

    [Fact]
    public void Generate_HashMatchesSha256OfRawToken()
    {
        var token = _generator.Generate();

        var expected = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token.RawToken))).ToLowerInvariant();
        Assert.Equal(expected, token.TokenHash);
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        var first = _generator.Hash("some-raw-token");
        var second = _generator.Hash("some-raw-token");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Hash_OfGeneratedRawToken_EqualsItsHash()
    {
        var token = _generator.Generate();

        Assert.Equal(token.TokenHash, _generator.Hash(token.RawToken));
    }
}
