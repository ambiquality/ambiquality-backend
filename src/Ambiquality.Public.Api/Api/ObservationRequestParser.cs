using Ambiquality.Public.Api.Application.Observations;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ambiquality.Public.Api.Api;

/// <summary>
/// Parses and validates the observation query string into an <see cref="ObservationFilter"/>,
/// shared by the JSON/JSON-LD list endpoint and the CSV export. Returns a 400
/// <see cref="ProblemHttpResult"/> on malformed input (and <c>filter</c> is undefined);
/// returns <c>null</c> on success.
/// </summary>
public static class ObservationRequestParser
{
    public static ProblemHttpResult? TryParse(HttpRequest request, out ObservationFilter filter)
    {
        filter = null!;
        var query = request.Query;

        if (Problems.TryParseUtcInstant(query["from"].FirstOrDefault(), "from", out var from) is { } fromError)
            return fromError;
        if (Problems.TryParseUtcInstant(query["to"].FirstOrDefault(), "to", out var to) is { } toError)
            return toError;

        if (TryGuid(query["sensorId"].FirstOrDefault(), "sensorId", out var sensorId) is { } sensorErr)
            return sensorErr;
        if (TryGuid(query["buildingId"].FirstOrDefault(), "buildingId", out var buildingId) is { } buildingErr)
            return buildingErr;
        if (TryGuid(query["roomId"].FirstOrDefault(), "roomId", out var roomId) is { } roomErr)
            return roomErr;

        BoundingBox? bbox = null;
        var bboxRaw = query["bbox"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(bboxRaw))
        {
            if (!BoundingBox.TryParse(bboxRaw, out var parsedBbox))
                return Problems.InvalidBbox();
            bbox = parsedBbox;
        }

        ObservationCursor? cursor = null;
        var cursorRaw = query["cursor"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cursorRaw))
        {
            if (!ObservationCursor.TryDecode(cursorRaw, out cursor))
                return Problems.InvalidCursor();
        }

        var includeInvalid = bool.TryParse(query["includeInvalid"].FirstOrDefault(), out var inv) && inv;
        var limit = ClampLimit(query["limit"].FirstOrDefault());
        var parameterCode = Trimmed(query["parameterCode"].FirstOrDefault());

        filter = new ObservationFilter(
            From: from,
            To: to,
            SensorId: sensorId,
            ParameterCode: parameterCode,
            BuildingId: buildingId,
            RoomId: roomId,
            Bbox: bbox,
            IncludeInvalid: includeInvalid,
            Limit: limit,
            Cursor: cursor);

        return null;
    }

    private static ProblemHttpResult? TryGuid(string? raw, string name, out Guid? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (Guid.TryParse(raw, out var guid))
        {
            value = guid;
            return null;
        }
        return Problems.BadRequest("Invalid identifier", $"The '{name}' query parameter must be a GUID.", "invalid-id");
    }

    private static int ClampLimit(string? raw)
    {
        if (!int.TryParse(raw, out var limit))
            return Constants.DefaultPageSize;
        return Math.Clamp(limit, 1, Constants.MaxPageSize);
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
