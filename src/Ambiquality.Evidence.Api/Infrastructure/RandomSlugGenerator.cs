using System.Security.Cryptography;
using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Infrastructure;

/// <summary>
/// Generates slugs as <c>{prefix}-{token}</c> where the token is 8 characters
/// drawn from a Crockford-style base32 alphabet (lowercase, no <c>i/l/o/u</c> to
/// avoid look-alikes). The 32^8 ≈ 1.1e12 keyspace makes collisions effectively
/// impossible at the platform's scale, so a handful of retries is ample.
/// </summary>
public sealed class RandomSlugGenerator : ISlugGenerator
{
    // Lowercase + digits only, so every token passes the UriSlug kebab-case regex.
    private const string Alphabet = "abcdefghjkmnpqrstvwxyz0123456789";
    private const int TokenLength = 8;
    private const int MaxAttempts = 5;

    public async Task<UriSlug> NextAsync(
        string prefix,
        Func<UriSlug, CancellationToken, Task<bool>> exists,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var token = RandomNumberGenerator.GetString(Alphabet, TokenLength);
            var candidate = UriSlug.Create($"{prefix}-{token}");
            if (!await exists(candidate, cancellationToken))
                return candidate;
        }

        throw new InvalidOperationException(
            $"Failed to generate a unique '{prefix}' slug after {MaxAttempts} attempts.");
    }
}
