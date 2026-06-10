namespace Ambiquality.Evidence.Api.Domain.Common;

/// <summary>
/// Enumeration-like value object capturing an indoor-environmental quantity a
/// sensor is capable of measuring. Backed by the
/// <c>evidence.measured_parameter</c> codelist.
/// </summary>
public sealed class MeasuredParameter : IEquatable<MeasuredParameter>
{
    // Gases — concentration
    public static readonly MeasuredParameter Co2          = new("co2");
    public static readonly MeasuredParameter ECo2         = new("eco2");
    public static readonly MeasuredParameter Co           = new("co");
    public static readonly MeasuredParameter O3           = new("o3");
    public static readonly MeasuredParameter No2          = new("no2");
    public static readonly MeasuredParameter So2          = new("so2");
    public static readonly MeasuredParameter Voc          = new("voc");

    // Particulate matter
    public static readonly MeasuredParameter Pm1          = new("pm1");
    public static readonly MeasuredParameter Pm2_5        = new("pm2_5");
    public static readonly MeasuredParameter Pm4          = new("pm4");
    public static readonly MeasuredParameter Pm10         = new("pm10");

    // Thermal comfort
    public static readonly MeasuredParameter Temperature  = new("temperature");
    public static readonly MeasuredParameter Humidity     = new("humidity");
    public static readonly MeasuredParameter AirVelocity  = new("air_velocity");
    public static readonly MeasuredParameter Pressure     = new("pressure");

    // Light
    public static readonly MeasuredParameter Illuminance  = new("illuminance");
    public static readonly MeasuredParameter Cct          = new("cct");

    // Acoustics
    public static readonly MeasuredParameter Laeq         = new("laeq");

    private static readonly Dictionary<string, MeasuredParameter> ByCode =
        new[]
        {
            Co2, ECo2, Co, O3, No2, So2, Voc,
            Pm1, Pm2_5, Pm4, Pm10,
            Temperature, Humidity, AirVelocity, Pressure,
            Illuminance, Cct,
            Laeq,
        }
        .ToDictionary(v => v.Code, StringComparer.OrdinalIgnoreCase);

    private MeasuredParameter(string code) => Code = code;

    public string Code { get; }

    public static MeasuredParameter FromCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (ByCode.TryGetValue(code, out var value))
            return value;

        throw new ArgumentException($"Unknown measured parameter code '{code}'.", nameof(code));
    }

    /// <summary>
    /// Registers an operator-defined parameter from the vocabulary-extensions file
    /// (POD-04) so sensors can declare it. Skips codes that already exist — a built-in
    /// can never be redefined. Called only during single-threaded startup.
    /// </summary>
    internal static void Register(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ByCode.TryAdd(code, new MeasuredParameter(code));
    }

    public bool Equals(MeasuredParameter? other) => other is not null && Code == other.Code;

    public override bool Equals(object? obj) => Equals(obj as MeasuredParameter);

    public override int GetHashCode() => Code.GetHashCode();

    public override string ToString() => Code;
}
