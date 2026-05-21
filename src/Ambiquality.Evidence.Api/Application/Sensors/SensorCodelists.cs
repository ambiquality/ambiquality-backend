using Ambiquality.Evidence.Api.Domain.Common;

namespace Ambiquality.Evidence.Api.Application.Sensors;

/// <summary>
/// Parses user-supplied status / measured-parameter codes into their codelist
/// value objects, translating an unknown code into the
/// <see cref="UnknownCodelistCodeException"/> the API maps to a 400.
/// </summary>
internal static class SensorCodelists
{
    public static SensorStatus ParseStatus(string code)
    {
        try
        {
            return SensorStatus.FromCode(code);
        }
        catch (ArgumentException)
        {
            throw new UnknownCodelistCodeException("sensor_status", code);
        }
    }

    public static MeasuredParameter ParseParameter(string code)
    {
        try
        {
            return MeasuredParameter.FromCode(code);
        }
        catch (ArgumentException)
        {
            throw new UnknownCodelistCodeException("measured_parameter", code);
        }
    }
}
