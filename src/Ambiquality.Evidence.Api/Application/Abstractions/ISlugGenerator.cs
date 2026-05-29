using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Application.Abstractions;

/// <summary>
/// Generates opaque, collision-resistant <see cref="UriSlug"/>s for newly
/// registered aggregates (e.g. <c>bld-7gk2qp</c>). The slug is server-owned —
/// clients never supply or influence it — so registration can never fail with a
/// "slug already in use" error.
/// </summary>
public interface ISlugGenerator
{
    /// <summary>
    /// Produces a fresh slug of the form <c>{prefix}-{token}</c>, retrying with a
    /// new random token until <paramref name="exists"/> reports the candidate is
    /// free. The database unique index remains the source of truth; this check
    /// only avoids the (astronomically rare) wasted insert attempt.
    /// </summary>
    /// <param name="prefix">Short type prefix, e.g. <c>bld</c>, <c>rm</c>, <c>sns</c>.</param>
    /// <param name="exists">Returns true when a candidate slug is already taken.</param>
    Task<UriSlug> NextAsync(
        string prefix,
        Func<UriSlug, CancellationToken, Task<bool>> exists,
        CancellationToken cancellationToken = default);
}
