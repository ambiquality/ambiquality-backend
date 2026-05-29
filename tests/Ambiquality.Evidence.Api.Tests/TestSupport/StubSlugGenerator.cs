using Ambiquality.Evidence.Api.Application.Abstractions;
using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Tests.TestSupport;

/// <summary>
/// Deterministic <see cref="ISlugGenerator"/> for handler unit tests: yields
/// <c>{prefix}-test0001</c>, <c>{prefix}-test0002</c>, … and honours the
/// <c>exists</c> callback so collision/retry behaviour can be exercised.
/// </summary>
public sealed class StubSlugGenerator : ISlugGenerator
{
    private int _counter;

    public async Task<UriSlug> NextAsync(
        string prefix,
        Func<UriSlug, CancellationToken, Task<bool>> exists,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var candidate = UriSlug.Create($"{prefix}-test{++_counter:0000}");
            if (!await exists(candidate, cancellationToken))
                return candidate;
        }
    }
}
