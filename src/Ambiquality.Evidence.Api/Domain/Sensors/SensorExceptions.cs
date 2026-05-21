namespace Ambiquality.Evidence.Api.Domain.Sensors;

/// <summary>Raised when a referenced sensor does not exist.</summary>
public sealed class SensorNotFoundException : DomainException
{
    public SensorNotFoundException(Guid sensorId)
        : base($"Sensor '{sensorId}' was not found.") { }
}

/// <summary>
/// Raised when removing a measured parameter that has no open history row on
/// the sensor — there is nothing to close.
/// </summary>
public sealed class MeasuredParameterNotFoundException : DomainException
{
    public MeasuredParameterNotFoundException(string parameterCode)
        : base($"No open measured parameter '{parameterCode}' was found on this sensor.") { }
}
