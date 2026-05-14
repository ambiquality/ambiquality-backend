namespace Ambiquality.Auth.Api.Application.Users;

/// <summary>
/// Builds the plain-text bodies for the email-confirmation and email-change
/// messages. Links point at the frontend, which forwards the parameters to the
/// API confirmation endpoints.
/// </summary>
internal static class ConfirmationEmail
{
    public static (string Subject, string Body) ForRegistration(
        string frontendBaseUrl, Guid userId, string rawToken)
    {
        var link = $"{frontendBaseUrl.TrimEnd('/')}/confirm-email?userId={userId}&token={rawToken}";
        var body =
            "Welcome to AmbiQuality.\n\n" +
            "Please confirm your email address by opening the link below:\n" +
            link + "\n\n" +
            "If you did not create this account, you can ignore this message.\n";
        return ("Confirm your AmbiQuality account", body);
    }

    public static (string Subject, string Body) ForEmailChange(
        string frontendBaseUrl, Guid userId, string rawToken)
    {
        var link = $"{frontendBaseUrl.TrimEnd('/')}/confirm-email-change?userId={userId}&token={rawToken}";
        var body =
            "A change of email address was requested for your AmbiQuality account.\n\n" +
            "Please confirm this new address by opening the link below:\n" +
            link + "\n\n" +
            "If you did not request this change, you can ignore this message.\n";
        return ("Confirm your new AmbiQuality email address", body);
    }
}
