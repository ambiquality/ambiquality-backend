using System.Net;
using System.Net.Http.Json;
using Ambiquality.Auth.Api.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Ambiquality.Auth.Api.Tests.Api;

/// <summary>
/// Boots the Auth.Api host with a deliberately tiny per-IP email-triggering
/// limit so the 429 path can be exercised in a few requests. Each test class
/// owns its own factory instance so the fixed-window budget is not shared.
/// </summary>
public sealed class RateLimitedEmailAuthApiFactory : AuthApiFactory
{
    public const int IpLimit = 3;

    protected override int EmailIpPermitLimit => IpLimit;
}

public class RegisterEmailRateLimitTests(RateLimitedEmailAuthApiFactory factory)
    : IClassFixture<RateLimitedEmailAuthApiFactory>
{
    [Fact]
    public async Task Register_OverIpLimit_Returns429WithRetryAfter()
    {
        var client = factory.CreateClient();

        // The first IpLimit registrations are permitted (here all 201).
        for (var i = 0; i < RateLimitedEmailAuthApiFactory.IpLimit; i++)
        {
            var allowed = await client.PostAsJsonAsync(
                "/v1/register", new RegisterRequest(UniqueEmail(), "Sup3rSecret!"));
            Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        }

        // The next attempt from the same IP is throttled.
        var limited = await client.PostAsJsonAsync(
            "/v1/register", new RegisterRequest(UniqueEmail(), "Sup3rSecret!"));

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(limited.Headers.RetryAfter);
        var problem = await limited.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("urn:ambiquality:auth:too-many-requests", problem!.Type);
    }

    private static string UniqueEmail() => $"rate-{Guid.NewGuid():N}@example.com";
}

public class ResendConfirmationEmailRateLimitTests(RateLimitedEmailAuthApiFactory factory)
    : IClassFixture<RateLimitedEmailAuthApiFactory>
{
    [Fact]
    public async Task ResendConfirmation_OverIpLimit_Returns429WithRetryAfter()
    {
        var client = factory.CreateClient();

        // resend-confirmation always answers 202 (anti-enumeration) within budget.
        for (var i = 0; i < RateLimitedEmailAuthApiFactory.IpLimit; i++)
        {
            var allowed = await client.PostAsJsonAsync(
                "/v1/resend-confirmation", new ResendConfirmationRequest(UniqueEmail()));
            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        }

        var limited = await client.PostAsJsonAsync(
            "/v1/resend-confirmation", new ResendConfirmationRequest(UniqueEmail()));

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(limited.Headers.RetryAfter);
        var problem = await limited.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("urn:ambiquality:auth:too-many-requests", problem!.Type);
    }

    private static string UniqueEmail() => $"rate-{Guid.NewGuid():N}@example.com";
}
