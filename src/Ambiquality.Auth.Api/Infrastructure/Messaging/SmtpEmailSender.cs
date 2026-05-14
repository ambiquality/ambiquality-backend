using Ambiquality.Auth.Api.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Ambiquality.Auth.Api.Infrastructure.Messaging;

/// <summary>
/// Sends plain-text transactional emails via MailKit's <see cref="SmtpClient"/>.
/// No HTML, no templating — bodies are built by the application layer.
/// </summary>
public sealed class SmtpEmailSender(SmtpOptions options) : IEmailSender
{
    public async Task SendAsync(
        string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();

        var socketOptions = options.UseStartTls
            ? SecureSocketOptions.StartTlsWhenAvailable
            : SecureSocketOptions.Auto;
        await client.ConnectAsync(options.Host, options.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrEmpty(options.Username))
            await client.AuthenticateAsync(
                options.Username, options.Password ?? string.Empty, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
