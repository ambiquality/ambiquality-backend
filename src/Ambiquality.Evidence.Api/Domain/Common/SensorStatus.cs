namespace Ambiquality.Evidence.Api.Domain.Common;

/// <summary>
/// Enumeration-like value object capturing a sensor's operational lifecycle
/// state. Backed by the <c>evidence.sensor_status</c> codelist.
/// </summary>
public sealed class SensorStatus : IEquatable<SensorStatus>
{
    public static readonly SensorStatus Active = new("active");
    public static readonly SensorStatus Maintenance = new("maintenance");
    public static readonly SensorStatus Decommissioned = new("decommissioned");

    private static readonly IReadOnlyDictionary<string, SensorStatus> ByCode =
        new[] { Active, Maintenance, Decommissioned }
            .ToDictionary(v => v.Code, StringComparer.OrdinalIgnoreCase);

    private SensorStatus(string code) => Code = code;

    public string Code { get; }

    public static SensorStatus FromCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (ByCode.TryGetValue(code, out var value))
            return value;

        throw new ArgumentException($"Unknown sensor status code '{code}'.", nameof(code));
    }

    public bool Equals(SensorStatus? other) => other is not null && Code == other.Code;

    public override bool Equals(object? obj) => Equals(obj as SensorStatus);

    public override int GetHashCode() => Code.GetHashCode();

    public override string ToString() => Code;
}
