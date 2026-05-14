namespace Ambiquality.Auth.Api.Application.Abstractions;

/// <summary>
/// A crypto-random raw token paired with the SHA-256 hash that gets persisted.
/// </summary>
public sealed record GeneratedToken(string RawToken, string TokenHash);

/// <summary>Generates crypto-random tokens and hashes raw tokens for lookup.</summary>
public interface ITokenGenerator
{
    GeneratedToken Generate();

    string Hash(string rawToken);
}
