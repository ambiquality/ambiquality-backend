namespace Ambiquality.Evidence.Api.Domain.Common;

/// <summary>
/// Enumeration-like value object capturing an indoor-environmental quantity a
/// sensor is capable of measuring. Backed by the
/// <c>evidence.measured_parameter</c> codelist.
/// </summary>
public sealed class MeasuredParameter : IEquatable<MeasuredParameter>
{
    public static readonly MeasuredParameter Co2 = new("co2");
    public static readonly MeasuredParameter Temperature = new("temperature");
    public static readonly MeasuredParameter Humidity = new("humidity");
    public static readonly MeasuredParameter ParticulateMatter = new("pm");
    public static readonly MeasuredParameter Voc = new("voc");
    public static readonly MeasuredParameter Acoustics = new("acoustics");
    public static readonly MeasuredParameter Light = new("light");

    private static readonly IReadOnlyDictionary<string, MeasuredParameter> ByCode =
        new[] { Co2, Temperature, Humidity, ParticulateMatter, Voc, Acoustics, Light }
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

    public bool Equals(MeasuredParameter? other) => other is not null && Code == other.Code;

    public override bool Equals(object? obj) => Equals(obj as MeasuredParameter);

    public override int GetHashCode() => Code.GetHashCode();

    public override string ToString() => Code;
}
