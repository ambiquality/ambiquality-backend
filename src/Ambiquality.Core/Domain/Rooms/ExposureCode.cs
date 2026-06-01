namespace Ambiquality.Core.Domain.Rooms;

/// <summary>
/// Codelist for a room's typical occupant-exposure (stay) duration. Stored as a
/// free-form <c>varchar</c> on <c>room_exposure_history</c>, so this is a plain
/// constant set rather than an EF-mapped value object.
/// </summary>
/// <remarks>
/// Lives in <c>Ambiquality.Core</c> because both Evidence.Api (validates the code
/// on write) and Public.Api (maps a <c>minExposure</c> minutes filter onto the
/// qualifying code set) need it. Ordering matters for the minutes mapping:
/// short ⊂ medium ⊂ long by typical stay length.
/// </remarks>
public static class ExposureCode
{
    /// <summary>Brief stays, roughly ≤ 30 minutes (e.g. corridors, lobbies).</summary>
    public const string Short = "short";

    /// <summary>Moderate stays, roughly ≤ 120 minutes (e.g. meeting rooms).</summary>
    public const string Medium = "medium";

    /// <summary>Prolonged stays, more than 120 minutes (e.g. offices, classrooms).</summary>
    public const string Long = "long";

    /// <summary>All permitted exposure codes; case-insensitive membership.</summary>
    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Short, Medium, Long };

    /// <summary>True when <paramref name="code"/> is a recognised exposure code.</summary>
    public static bool IsValid(string code) => All.Contains(code);
}
