namespace Ambiquality.Core.Domain.Measurements;

/// <summary>
/// Codelist row defining the permissible value range for a measured quantity.
/// Used by the ingestion service to reject out-of-range observations (UC10 step B).
/// Stored in the <c>ieq.parameter_ranges</c> table so ranges are configurable
/// without a redeploy.
/// </summary>
public sealed class ParameterRange
{
    private ParameterRange()
    {
        ParameterCode = null!;
    }

    public string ParameterCode { get; private set; }
    public double MinValue { get; private set; }
    public double MaxValue { get; private set; }

    /// <summary>Canonical unit for the quantity; informational until F08 unit matching lands.</summary>
    public string? Unit { get; private set; }

    public ParameterRange(string parameterCode, double minValue, double maxValue, string? unit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterCode);
        if (maxValue < minValue)
            throw new ArgumentException("maxValue must be greater than or equal to minValue.", nameof(maxValue));

        ParameterCode = parameterCode;
        MinValue = minValue;
        MaxValue = maxValue;
        Unit = unit;
    }

    public bool Contains(double value) => value >= MinValue && value <= MaxValue;
}
