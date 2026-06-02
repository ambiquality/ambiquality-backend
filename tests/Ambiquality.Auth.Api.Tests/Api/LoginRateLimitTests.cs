using System.Net;
using System.Net.Http.Json;
using Ambiquality.Auth.Api.Api.Contracts;

namespace Ambiquality.Auth.Api.Tests.Api;

/// <summary>
/// Boots the Auth.Api host with a deliberately tiny per-IP login limit so the
/// 429 path can be exercised in a few requests.
/// </summary>
public sealed class RateLimitedAuthApiFactory : AuthApiFactory
{
    public const int IpLimit = 3;

    protected override int LoginIpPermitLimit => IpLimit;
}

public class LoginRateLimitTests(RateLimitedAuthApiFactory factory)
    : IClassFixture<RateLimitedAuthApiFactory>
{
    [Fact]
    public async Task Login_OverIpLimit_Returns429WithRetryAfter()
    {
        var client = factory.CreateClient();

        // The first IpLimit attempts are permitted (here all 401 — unknown email).
        for (var i = 0; i < RateLimitedAuthApiFactory.IpLimit; i++)
        {
            var allowed = await client.PostAsJsonAsync(
                "/login", new LoginRequest($"nobody-{Guid.NewGuid():N}@example.com", "whatever"));
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        // The next attempt from the same IP is throttled.
        var limited = await client.PostAsJsonAsync(
            "/login", new LoginRequest($"nobody-{Guid.NewGuid():N}@example.com", "whatever"));

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(limited.Headers.RetryAfter);
    }
}
