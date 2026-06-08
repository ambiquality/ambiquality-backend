using Npgsql;

namespace Ambiquality.Public.Api.Tests.Infrastructure;

/// <summary>
/// Seeds a known dataset via raw SQL (exercising the same schema the catalog reads):
/// one building, one room, one sensor, and five measurements — including a pair that
/// share a <c>received_at</c> instant so the keyset tie-break can be verified, and
/// one invalidated row.
/// </summary>
public static class EvidenceSeed
{
    public const string BuildingId = "11111111-1111-1111-1111-111111111111";
    public const string RoomId = "22222222-2222-2222-2222-222222222222";
    public const string SensorId = "33333333-3333-3333-3333-333333333333";
    public const string BuildingStreetId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    public const string BuildingMunicipalityId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
    private const string RecordedBy = "99999999-9999-9999-9999-999999999999";

    // co2 measurements: M2 and M3 share received_at T2 (the tie-break pair).
    public const string M1 = "00000000-0000-0000-0000-000000000001"; // co2 400, T1
    public const string M2 = "00000000-0000-0000-0000-000000000002"; // co2 410, T2
    public const string M3 = "00000000-0000-0000-0000-000000000003"; // co2 420, T2 (tie)
    public const string M4 = "00000000-0000-0000-0000-000000000004"; // co2 430, T3, INVALID
    public const string M5 = "00000000-0000-0000-0000-000000000005"; // temperature, T4

    public static async Task SeedAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(Sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private const string Validity = "tstzrange('2026-01-01 00:00:00+00', NULL)";

    private static readonly string Sql = $"""
        -- Building
        INSERT INTO evidence.buildings ("Id", uri_slug, owner_id, created_by, created_at)
        VALUES ('{BuildingId}', 'bld-test', '{RecordedBy}', '{RecordedBy}', '2026-01-01 00:00:00+00');

        INSERT INTO evidence.building_name_history (building_id, name, validity, recorded_at, recorded_by)
        VALUES ('{BuildingId}', 'Test Tower', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        INSERT INTO evidence.building_address_history (building_id, address_point_code, street_name, house_number, house_number_type, orientation_number, orientation_number_letter, municipality_name, municipality_part_name, psc, district_name, region_name, validity, recorded_at, recorded_by)
        VALUES ('{BuildingId}', 70010001, 'Karlovo náměstí', 1, 'č.p.', 1, NULL, 'Praha', 'Nové Město', '11000', 'Hlavní město Praha', 'Hlavní město Praha', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        INSERT INTO evidence.building_type_history (building_id, building_type_code, validity, recorded_at, recorded_by)
        VALUES ('{BuildingId}', 'office', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        INSERT INTO evidence.building_location_history (building_id, latitude, longitude, validity, recorded_at, recorded_by)
        VALUES ('{BuildingId}', 50.087465, 14.421253, {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        INSERT INTO evidence.building_years_history (building_id, year_built, year_renovated, validity, recorded_at, recorded_by)
        VALUES ('{BuildingId}', 2000, 2015, {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        -- Room
        INSERT INTO evidence.rooms ("Id", building_id, uri_slug, created_by, created_at)
        VALUES ('{RoomId}', '{BuildingId}', 'rm-test', '{RecordedBy}', '2026-01-01 00:00:00+00');

        INSERT INTO evidence.room_name_history (room_id, name, validity, recorded_at, recorded_by)
        VALUES ('{RoomId}', 'Lab 1', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        INSERT INTO evidence.room_floor_history (room_id, floor, validity, recorded_at, recorded_by)
        VALUES ('{RoomId}', 2, {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        INSERT INTO evidence.room_function_history (room_id, function_code, validity, recorded_at, recorded_by)
        VALUES ('{RoomId}', 'office', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        INSERT INTO evidence.room_exposure_history (room_id, exposure_code, validity, recorded_at, recorded_by)
        VALUES ('{RoomId}', 'long', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        INSERT INTO evidence.room_geometry_history (room_id, area_m2, ceiling_height_m, validity, recorded_at, recorded_by)
        VALUES ('{RoomId}', 25.5, 3.0, {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        INSERT INTO evidence.room_ventilation_history (room_id, ventilation_type, validity, recorded_at, recorded_by)
        VALUES ('{RoomId}', 'mechanical', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        INSERT INTO evidence.room_pollution_source_history (room_id, source_code, validity, recorded_at, recorded_by)
        VALUES ('{RoomId}', 'traffic', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        -- Sensor
        INSERT INTO evidence.sensors ("Id", uri_slug, current_building_id, current_room_id, created_by, created_at, api_key_hash)
        VALUES ('{SensorId}', 'sn-test', '{BuildingId}', '{RoomId}', '{RecordedBy}', '2026-01-01 00:00:00+00', repeat('a', 64));

        INSERT INTO evidence.sensor_identity_history (sensor_id, manufacturer, model, serial_number, validity, recorded_at, recorded_by)
        VALUES ('{SensorId}', 'Acme', 'X1', 'SN-1', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        INSERT INTO evidence.sensor_status_history (sensor_id, status_code, validity, recorded_at, recorded_by)
        VALUES ('{SensorId}', 'active', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        INSERT INTO evidence.sensor_measured_parameter_history (sensor_id, parameter_code, validity, recorded_at, recorded_by)
        VALUES ('{SensorId}', 'co2', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}'),
               ('{SensorId}', 'temperature', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        INSERT INTO evidence.sensor_placement_history (sensor_id, building_id, room_id, validity, recorded_at, recorded_by)
        VALUES ('{SensorId}', '{BuildingId}', '{RoomId}', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        -- Building 2 (Praha — Václavské náměstí)
        INSERT INTO evidence.buildings ("Id", uri_slug, owner_id, created_by, created_at)
        VALUES ('{BuildingStreetId}', 'bld-street', '{RecordedBy}', '{RecordedBy}', '2026-01-01 00:00:00+00');
        INSERT INTO evidence.building_address_history (building_id, address_point_code, street_name, house_number, house_number_type, orientation_number, orientation_number_letter, municipality_name, municipality_part_name, psc, district_name, region_name, validity, recorded_at, recorded_by)
        VALUES ('{BuildingStreetId}', 70010002, 'Václavské náměstí', 837, 'č.p.', 56, NULL, 'Praha', 'Nové Město', '11000', 'Hlavní město Praha', 'Hlavní město Praha', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');
        INSERT INTO evidence.building_location_history (building_id, latitude, longitude, validity, recorded_at, recorded_by)
        VALUES ('{BuildingStreetId}', 50.081234, 14.427891, {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        -- Building 3 (Praha — Malostranské náměstí)
        INSERT INTO evidence.buildings ("Id", uri_slug, owner_id, created_by, created_at)
        VALUES ('{BuildingMunicipalityId}', 'bld-municipality', '{RecordedBy}', '{RecordedBy}', '2026-01-01 00:00:00+00');
        INSERT INTO evidence.building_address_history (building_id, address_point_code, street_name, house_number, house_number_type, orientation_number, orientation_number_letter, municipality_name, municipality_part_name, psc, district_name, region_name, validity, recorded_at, recorded_by)
        VALUES ('{BuildingMunicipalityId}', 70010003, 'Malostranské náměstí', 1, 'č.p.', 1, NULL, 'Praha', 'Malá Strana', '11800', 'Hlavní město Praha', 'Hlavní město Praha', {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');
        INSERT INTO evidence.building_location_history (building_id, latitude, longitude, validity, recorded_at, recorded_by)
        VALUES ('{BuildingMunicipalityId}', 50.088765, 14.403210, {Validity}, '2026-01-01 00:00:00+00', '{RecordedBy}');

        -- Measurements (M2 and M3 share received_at)
        INSERT INTO ieq.measurements (id, sensor_id, parameter_code, value, unit, observed_at, received_at, is_invalid)
        VALUES
          ('{M1}', '{SensorId}', 'co2', 400, 'ppm', '2026-05-01 10:00:00+00', '2026-05-01 10:00:00+00', false),
          ('{M2}', '{SensorId}', 'co2', 410, 'ppm', '2026-05-01 11:00:00+00', '2026-05-01 11:00:00+00', false),
          ('{M3}', '{SensorId}', 'co2', 420, 'ppm', '2026-05-01 11:00:00+00', '2026-05-01 11:00:00+00', false),
          ('{M4}', '{SensorId}', 'co2', 430, 'ppm', '2026-05-01 12:00:00+00', '2026-05-01 12:00:00+00', true),
          ('{M5}', '{SensorId}', 'temperature', 21.5, '°C', '2026-05-01 13:00:00+00', '2026-05-01 13:00:00+00', false);
        """;
}
