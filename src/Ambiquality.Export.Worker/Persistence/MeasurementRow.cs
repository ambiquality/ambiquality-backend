namespace Ambiquality.Export.Worker.Persistence;

/// <summary>One measurement row as read from the <c>ieq.measurements</c> hypertable.</summary>
public readonly record struct MeasurementRow(
    Guid Id,
    Guid SensorId,
    string ParameterCode,
    double Value,
    string? Unit,
    DateTime ObservedAt,
    DateTime ReceivedAt,
    bool IsInvalid);
