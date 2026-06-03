using Ambiquality.Core.Domain.Rooms;
using Ambiquality.Public.Api.Api;
using Npgsql;
using NpgsqlTypes;

namespace Ambiquality.Public.Api.Infrastructure.Catalog;

/// <summary>
/// Raw read-only access to the evidence catalog. Current-state attributes are the
/// open temporal rows (<c>upper_inf(validity)</c>); collections are aggregated with
/// <c>array_agg</c>; list totals come from a <c>COUNT(*) OVER()</c> window so paging
/// needs a single round trip. Numeric columns are cast to <c>float8</c> so the reader
/// yields <c>double</c> directly. Each projection is split into a column list and a
/// FROM/JOIN clause so the total-count column can be added for list queries only.
/// </summary>
public sealed class EvidenceCatalog(NpgsqlDataSource dataSource) : IEvidenceCatalog
{
    // ---- Building projection (12 columns: ordinals 0..11) -------------------

    private const string BuildingColumns = """
        b."Id",
        nh.name,
        ah.street, ah.city, ah.postcode, ah.country,
        th.building_type_code,
        lh.latitude::float8  AS latitude,
        lh.longitude::float8 AS longitude,
        lh.anonymization,
        yh.year_built::int    AS year_built,
        yh.year_renovated::int AS year_renovated
        """;

    private const string BuildingFrom = """
        FROM evidence.buildings b
        LEFT JOIN evidence.building_name_history     nh ON nh.building_id = b."Id" AND upper_inf(nh.validity)
        LEFT JOIN evidence.building_address_history  ah ON ah.building_id = b."Id" AND upper_inf(ah.validity)
        LEFT JOIN evidence.building_type_history     th ON th.building_id = b."Id" AND upper_inf(th.validity)
        LEFT JOIN evidence.building_location_history lh ON lh.building_id = b."Id" AND upper_inf(lh.validity)
        LEFT JOIN evidence.building_years_history    yh ON yh.building_id = b."Id" AND upper_inf(yh.validity)
        """;

    // ---- Room projection (10 columns: ordinals 0..9) ------------------------

    private const string RoomColumns = """
        r."Id", r.building_id,
        nh.name,
        fh.floor::int AS floor,
        fnh.function_code,
        eh.exposure_code,
        gh.area_m2::float8          AS area_m2,
        gh.ceiling_height_m::float8 AS ceiling_height_m,
        vh.ventilation_type,
        COALESCE((SELECT array_agg(psh.source_code)
                  FROM evidence.room_pollution_source_history psh
                  WHERE psh.room_id = r."Id" AND upper_inf(psh.validity)), '{}') AS pollution_sources
        """;

    private const string RoomFrom = """
        FROM evidence.rooms r
        LEFT JOIN evidence.room_name_history        nh  ON nh.room_id  = r."Id" AND upper_inf(nh.validity)
        LEFT JOIN evidence.room_floor_history       fh  ON fh.room_id  = r."Id" AND upper_inf(fh.validity)
        LEFT JOIN evidence.room_function_history    fnh ON fnh.room_id = r."Id" AND upper_inf(fnh.validity)
        LEFT JOIN evidence.room_exposure_history    eh  ON eh.room_id  = r."Id" AND upper_inf(eh.validity)
        LEFT JOIN evidence.room_geometry_history    gh  ON gh.room_id  = r."Id" AND upper_inf(gh.validity)
        LEFT JOIN evidence.room_ventilation_history vh  ON vh.room_id  = r."Id" AND upper_inf(vh.validity)
        """;

    // ---- Sensor projection (8 columns: ordinals 0..7) -----------------------

    private const string SensorColumns = """
        s."Id", s.current_building_id, s.current_room_id,
        ih.manufacturer, ih.model, ih.serial_number,
        sh.status_code,
        COALESCE((SELECT array_agg(mph.parameter_code)
                  FROM evidence.sensor_measured_parameter_history mph
                  WHERE mph.sensor_id = s."Id" AND upper_inf(mph.validity)), '{}') AS parameter_codes
        """;

    private const string SensorFrom = """
        FROM evidence.sensors s
        LEFT JOIN evidence.sensor_identity_history ih ON ih.sensor_id = s."Id" AND upper_inf(ih.validity)
        LEFT JOIN evidence.sensor_status_history   sh ON sh.sensor_id = s."Id" AND upper_inf(sh.validity)
        """;

    // ---- Buildings ----------------------------------------------------------

    public async Task<(IReadOnlyList<BuildingRow> Rows, long Total)> GetBuildingsAsync(
        string? buildingType, BoundingBox? bbox, int page, int pageSize, CancellationToken ct)
    {
        var sql = $"""
            SELECT {BuildingColumns}, COUNT(*) OVER() AS total_count
            {BuildingFrom}
            WHERE (@type IS NULL OR th.building_type_code = @type)
              AND (@hasBbox = FALSE OR (lh.longitude BETWEEN @minLon AND @maxLon
                                        AND lh.latitude BETWEEN @minLat AND @maxLat))
            ORDER BY b."Id"
            OFFSET @offset LIMIT @limit
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(Text("type", buildingType));
        AddBbox(command, bbox);
        AddPaging(command, page, pageSize);

        var rows = new List<BuildingRow>();
        long total = 0;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(MapBuilding(reader));
            total = reader.GetInt64(12);
        }
        return (rows, total);
    }

    public async Task<BuildingRow?> GetBuildingAsync(Guid id, CancellationToken ct)
    {
        var sql = $"""
            SELECT {BuildingColumns}
            {BuildingFrom}
            WHERE b."Id" = @id
            """;
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(Uuid("id", id));

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapBuilding(reader) : null;
    }

    private static BuildingRow MapBuilding(NpgsqlDataReader r) => new(
        Id: r.GetGuid(0),
        Name: NullableString(r, 1),
        Street: NullableString(r, 2),
        City: NullableString(r, 3),
        Postcode: NullableString(r, 4),
        Country: NullableString(r, 5),
        BuildingTypeCode: NullableString(r, 6),
        Latitude: NullableDouble(r, 7),
        Longitude: NullableDouble(r, 8),
        Anonymization: NullableString(r, 9),
        YearBuilt: NullableInt(r, 10),
        YearRenovated: NullableInt(r, 11));

    // ---- Rooms --------------------------------------------------------------

    public async Task<(IReadOnlyList<RoomRow> Rows, long Total)> GetRoomsAsync(
        Guid buildingId, string? functionCode, int? minExposureMinutes, int page, int pageSize, CancellationToken ct)
    {
        var sql = $"""
            SELECT {RoomColumns}, COUNT(*) OVER() AS total_count
            {RoomFrom}
            WHERE r.building_id = @buildingId
              AND (@function IS NULL OR fnh.function_code = @function)
              AND (@codes IS NULL OR eh.exposure_code = ANY(@codes))
            ORDER BY r."Id"
            OFFSET @offset LIMIT @limit
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(Uuid("buildingId", buildingId));
        command.Parameters.Add(Text("function", functionCode));
        command.Parameters.Add(TextArray("codes", ExposureCodesAtLeast(minExposureMinutes)));
        AddPaging(command, page, pageSize);

        var rows = new List<RoomRow>();
        long total = 0;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(MapRoom(reader));
            total = reader.GetInt64(10);
        }
        return (rows, total);
    }

    public async Task<RoomRow?> GetRoomAsync(Guid id, CancellationToken ct)
    {
        var sql = $"""
            SELECT {RoomColumns}
            {RoomFrom}
            WHERE r."Id" = @id
            """;
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(Uuid("id", id));

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRoom(reader) : null;
    }

    private static RoomRow MapRoom(NpgsqlDataReader r) => new(
        Id: r.GetGuid(0),
        BuildingId: r.GetGuid(1),
        Name: NullableString(r, 2),
        Floor: (byte)(NullableInt(r, 3) ?? 0),
        FunctionCode: NullableString(r, 4),
        ExposureCode: NullableString(r, 5),
        AreaM2: NullableDouble(r, 6),
        CeilingHeightM: NullableDouble(r, 7),
        VentilationType: NullableString(r, 8),
        PollutionSources: r.GetFieldValue<string[]>(9));

    // ---- Sensors ------------------------------------------------------------

    public async Task<(IReadOnlyList<SensorRow> Rows, long Total)> GetSensorsAsync(
        Guid roomId, string? parameterCode, string? status, int page, int pageSize, CancellationToken ct)
    {
        var sql = $"""
            SELECT {SensorColumns}, COUNT(*) OVER() AS total_count
            {SensorFrom}
            WHERE s.current_room_id = @roomId
              AND (@status IS NULL OR sh.status_code = @status)
              AND (@pc IS NULL OR EXISTS (
                      SELECT 1 FROM evidence.sensor_measured_parameter_history mph2
                      WHERE mph2.sensor_id = s."Id" AND mph2.parameter_code = @pc AND upper_inf(mph2.validity)))
            ORDER BY s."Id"
            OFFSET @offset LIMIT @limit
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(Uuid("roomId", roomId));
        command.Parameters.Add(Text("status", status));
        command.Parameters.Add(Text("pc", parameterCode));
        AddPaging(command, page, pageSize);

        var rows = new List<SensorRow>();
        long total = 0;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(MapSensor(reader));
            total = reader.GetInt64(8);
        }
        return (rows, total);
    }

    public async Task<SensorRow?> GetSensorAsync(Guid id, CancellationToken ct)
    {
        var sql = $"""
            SELECT {SensorColumns}
            {SensorFrom}
            WHERE s."Id" = @id
            """;
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(Uuid("id", id));

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapSensor(reader) : null;
    }

    private static SensorRow MapSensor(NpgsqlDataReader r) => new(
        Id: r.GetGuid(0),
        BuildingId: r.GetGuid(1),
        RoomId: r.GetGuid(2),
        Manufacturer: NullableString(r, 3),
        Model: NullableString(r, 4),
        SerialNumber: NullableString(r, 5),
        StatusCode: NullableString(r, 6),
        MeasuredParameterCodes: r.GetFieldValue<string[]>(7));

    // ---- Cross-cutting ------------------------------------------------------

    public async Task<IReadOnlyCollection<Guid>> ResolveSensorIdsAsync(
        Guid? buildingId, Guid? roomId, BoundingBox? bbox, CancellationToken ct)
    {
        const string sql = """
            SELECT s."Id"
            FROM evidence.sensors s
            LEFT JOIN evidence.building_location_history lh
              ON lh.building_id = s.current_building_id AND upper_inf(lh.validity)
            WHERE (@buildingId IS NULL OR s.current_building_id = @buildingId)
              AND (@roomId IS NULL OR s.current_room_id = @roomId)
              AND (@hasBbox = FALSE OR (lh.longitude BETWEEN @minLon AND @maxLon
                                        AND lh.latitude BETWEEN @minLat AND @maxLat))
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(Uuid("buildingId", buildingId));
        command.Parameters.Add(Uuid("roomId", roomId));
        AddBbox(command, bbox);

        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetGuid(0));
        return ids;
    }

    public async Task<IReadOnlyList<SensorPlacement>> GetSensorPlacementsAsync(
        IReadOnlyCollection<Guid> sensorIds, CancellationToken ct)
    {
        if (sensorIds.Count == 0)
            return [];

        // Every placement period (open and closed) for the requested sensors; the caller
        // picks the one covering each observation time. lower()/upper() unpack the tstzrange.
        const string sql = """
            SELECT sensor_id, room_id, building_id, lower(validity), upper(validity)
            FROM evidence.sensor_placement_history
            WHERE sensor_id = ANY(@ids)
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
            { Value = sensorIds.ToArray() });

        var rows = new List<SensorPlacement>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new SensorPlacement(
                SensorId: reader.GetGuid(0),
                RoomId: reader.GetGuid(1),
                BuildingId: reader.GetGuid(2),
                ValidFrom: reader.GetDateTime(3),
                ValidTo: reader.IsDBNull(4) ? null : reader.GetDateTime(4)));
        return rows;
    }

    public async Task<SpatialExtent?> GetSpatialExtentAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT MIN(lh.latitude)::float8, MIN(lh.longitude)::float8,
                   MAX(lh.latitude)::float8, MAX(lh.longitude)::float8
            FROM evidence.building_location_history lh
            WHERE upper_inf(lh.validity) AND lh.latitude IS NOT NULL AND lh.longitude IS NOT NULL
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || reader.IsDBNull(0))
            return null;

        return new SpatialExtent(reader.GetDouble(0), reader.GetDouble(1), reader.GetDouble(2), reader.GetDouble(3));
    }

    // ---- Helpers ------------------------------------------------------------

    private static string[]? ExposureCodesAtLeast(int? minutes) => minutes switch
    {
        null => null,
        <= 30 => [ExposureCode.Short, ExposureCode.Medium, ExposureCode.Long],
        <= 120 => [ExposureCode.Medium, ExposureCode.Long],
        _ => [ExposureCode.Long]
    };

    private static NpgsqlParameter Text(string name, string? value) =>
        new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };

    private static NpgsqlParameter TextArray(string name, string[]? value) =>
        new(name, NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };

    private static NpgsqlParameter Uuid(string name, Guid? value) =>
        new(name, NpgsqlDbType.Uuid) { Value = (object?)value ?? DBNull.Value };

    private static void AddPaging(NpgsqlCommand command, int page, int pageSize)
    {
        var offset = checked((page - 1) * pageSize);
        command.Parameters.Add(new NpgsqlParameter("offset", NpgsqlDbType.Integer) { Value = offset });
        command.Parameters.Add(new NpgsqlParameter("limit", NpgsqlDbType.Integer) { Value = pageSize });
    }

    private static void AddBbox(NpgsqlCommand command, BoundingBox? bbox)
    {
        command.Parameters.Add(new NpgsqlParameter("hasBbox", NpgsqlDbType.Boolean) { Value = bbox.HasValue });
        command.Parameters.Add(new NpgsqlParameter("minLon", NpgsqlDbType.Double) { Value = bbox?.MinLon ?? 0d });
        command.Parameters.Add(new NpgsqlParameter("minLat", NpgsqlDbType.Double) { Value = bbox?.MinLat ?? 0d });
        command.Parameters.Add(new NpgsqlParameter("maxLon", NpgsqlDbType.Double) { Value = bbox?.MaxLon ?? 0d });
        command.Parameters.Add(new NpgsqlParameter("maxLat", NpgsqlDbType.Double) { Value = bbox?.MaxLat ?? 0d });
    }

    private static string? NullableString(NpgsqlDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? null : r.GetString(ordinal);

    private static double? NullableDouble(NpgsqlDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? null : r.GetDouble(ordinal);

    private static int? NullableInt(NpgsqlDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? null : r.GetInt32(ordinal);
}
