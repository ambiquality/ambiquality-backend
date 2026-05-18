namespace Ambiquality.Evidence.Api.Domain.Common;

/// <summary>
/// Enumeration-like value object capturing how precisely a building's
/// location may be shared publicly. Backed by the
/// <c>evidence.anonymization_level</c> codelist.
/// </summary>
public sealed class AnonymizationLevel : IEquatable<AnonymizationLevel>
{
    public static readonly AnonymizationLevel Precise = new("precise");
    public static readonly AnonymizationLevel Street = new("street");
    public static readonly AnonymizationLevel Municipality = new("municipality");

    private static readonly IReadOnlyDictionary<string, AnonymizationLevel> ByCode =
        new[] { Precise, Street, Municipality }
            .ToDictionary(v => v.Code, StringComparer.OrdinalIgnoreCase);

    private AnonymizationLevel(string code) => Code = code;

    public string Code { get; }

    public static AnonymizationLevel FromCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (ByCode.TryGetValue(code, out var value))
            return value;

        throw new ArgumentException(
            $"Unknown anonymization level code '{code}'.", nameof(code));
    }

    public bool Equals(AnonymizationLevel? other)
        => other is not null && Code == other.Code;

    public override bool Equals(object? obj) => Equals(obj as AnonymizationLevel);

    public override int GetHashCode() => Code.GetHashCode();

    public override string ToString() => Code;
}
