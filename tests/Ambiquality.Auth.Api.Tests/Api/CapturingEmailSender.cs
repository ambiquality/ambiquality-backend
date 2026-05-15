using System.Collections.Concurrent;
using Ambiquality.Auth.Api.Application.Abstractions;

namespace Ambiquality.Auth.Api.Tests.Api;

/// <summary>Captures sent emails in memory so integration tests can read tokens.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    public sealed record SentEmail(string To, string Subject, string Body);

    public ConcurrentBag<SentEmail> Sent { get; } = [];

    public Task SendAsync(
        string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        Sent.Add(new SentEmail(to, subject, body));
        return Task.CompletedTask;
    }

    public SentEmail? LastTo(string address)
        => Sent.Where(e => e.To == address.ToLowerInvariant())
            .OrderBy(_ => 0)
            .LastOrDefault();

    /// <summary>Pulls the <c>token</c> query value out of a captured confirmation link.</summary>
    public static string ExtractToken(string body)
    {
        var marker = "token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            throw new InvalidOperationException("No token found in email body.");
        start += marker.Length;
        var end = body.IndexOfAny([' ', '\n', '\r', '&'], start);
        return end < 0 ? body[start..] : body[start..end];
    }
}
