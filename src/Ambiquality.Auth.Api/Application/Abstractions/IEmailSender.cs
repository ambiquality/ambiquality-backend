namespace Ambiquality.Auth.Api.Application.Abstractions;

/// <summary>Sends plain-text transactional emails.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
