namespace Ambiquality.Evidence.Api.Api;

/// <summary>Cross-cutting API constants for the evidence service.</summary>
public static class Constants
{
    /// <summary>
    /// API version segment. Every route is mounted under <c>/{ApiVersion}</c>
    /// (see <c>Program.cs</c>), uniform with the other Ambiquality services.
    /// </summary>
    public const string ApiVersion = "v1";
}
