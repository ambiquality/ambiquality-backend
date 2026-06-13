namespace Ambiquality.Auth.Api.Infrastructure.Messaging;

/// <summary>SMTP delivery settings, bound from the <c>Smtp</c> config section.</summary>
public sealed class SmtpOptions
{
    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 25;

    public string? Username { get; init; }

    public string? Password { get; init; }

    public bool UseStartTls { get; init; } = true;

    public string FromAddress { get; init; } = "no-reply@ambiquality.org";

    public string FromName { get; init; } = "AmbiQuality";
}
