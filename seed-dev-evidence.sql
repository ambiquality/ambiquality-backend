-- Seed data for evidence database (development only)
-- Creates:
-- - 2 user projections (alice, bob)
-- - 3 buildings (Prague, Berlin, Brno) with one each of precise/street/municipality anonymization
-- - 10 rooms total
-- - 15 sensors covering all 18 parameters
--
-- Sensor API Keys (plaintext → SHA-256):
-- seed-sensor-prague-classroom-201 → de64f9e4d3cdf3eb7d0d4edfbf4266b2c52d21da6baced5b1442a14788c3ec69
-- seed-sensor-prague-conference-202 → c3be7f60d18505588e6e06847761780b1004c1d135786b4db6d1d64fd445e122
-- seed-sensor-prague-lab-301 → d62c8ca70e28e6981d51e94602b4aa411625e8672b46b8efd8b313298b551f6b
-- seed-sensor-prague-corridor-2 → 18eb509321c0a773d1a2aaf3ce77a790e1ae1a3398202368788230dab4fa1658
-- seed-sensor-berlin-open-office-501 → 4fdf1c81e03589c08ddb3cd215023662873a53a49a6c7237899d3f83587604f0
-- seed-sensor-berlin-meeting-502 → 4f7bb687ae90c7e759c2e8182e84156f31394d5b48b1efc08f1d3f2fdcf0a951
-- seed-sensor-berlin-kitchen-503 → fcbcdabb36e38177350a4e1460776ce033323647eda43b2566b715b56c4becc2
-- seed-sensor-brno-classroom-101 → 895fa1f44eaa707132914420162b09eb0f7e0236c11e5c661a453b2e7f8d9068
-- seed-sensor-brno-office-201 → 5590d0fd6ff8825ff9f7fd5ab40d9952ea25d5189e37ab90789c18736de6704d
-- seed-sensor-brno-storage-b1 → fc253df3fb1838895d6f59eadb884daadb95a0764e1d4a7b55034d17b07e81a4
-- seed-sensor-prague-multi-gas → f024b890cf06b531dd2af4114591fd8be580ec14d33608da18d1353aa1676bdf
-- seed-sensor-prague-light-1 → 1f301c0a55660af48f7cb8cb199ba1f3deec953716660039a1815ece4733ddb5
-- seed-sensor-berlin-light-2 → 87eaa08d639f833699b4e6bfc26c4dbc0941b5ab2622d70bbb670a050c70aca3
-- seed-sensor-prague-acoustic → beab478a5483799cf833a5ee7bdcf4e172a6b864774a9b50c6f84c3062acbe85
-- seed-sensor-berlin-acoustic → 2b96d6faad058da07733b292fa6728737792aa1e0515974a2527e3bfed68d1f5

DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM evidence.buildings) THEN
    RAISE NOTICE 'Evidence seed already applied, skipping.';
    RETURN;
  END IF;

  -- ============================================================================
  -- User Projections
  -- ============================================================================

  INSERT INTO evidence.user_projections ("Id", auth_user_id, created_at) VALUES
    ('30000000-0000-0000-0000-000000000001'::uuid, '10000000-0000-0000-0000-000000000001'::uuid, NOW()),
    ('40000000-0000-0000-0000-000000000001'::uuid, '20000000-0000-0000-0000-000000000001'::uuid, NOW());

  -- ============================================================================
  -- Buildings (3 total: Prague precise, Berlin street, Brno municipality)
  -- ============================================================================

  -- Prague Building
  INSERT INTO evidence.buildings ("Id", uri_slug, owner_id, created_by, created_at) VALUES
    ('50100000-0000-0000-0000-000000000001'::uuid, 'prague-vse-fis', '30000000-0000-0000-0000-000000000001'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW());

  INSERT INTO evidence.building_name_history (building_id, recorded_at, recorded_by, validity, name) VALUES
    ('50100000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'VŠE Prague - Faculty of Informatics and Statistics');

  INSERT INTO evidence.building_address_history (building_id, recorded_at, recorded_by, validity, street, city, postcode, country) VALUES
    ('50100000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Nám. W. Churchilla 4', 'Prague', '13067', 'CZ');

  INSERT INTO evidence.building_type_history (building_id, recorded_at, recorded_by, validity, building_type_code) VALUES
    ('50100000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'educational');

  INSERT INTO evidence.building_location_history (building_id, recorded_at, recorded_by, validity, latitude, longitude, anonymization) VALUES
    ('50100000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 50.079167, 14.433056, 'precise');

  INSERT INTO evidence.building_years_history (building_id, recorded_at, recorded_by, validity, year_built, year_renovated) VALUES
    ('50100000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 1964, 2018);

  -- Berlin Building
  INSERT INTO evidence.buildings ("Id", uri_slug, owner_id, created_by, created_at) VALUES
    ('50200000-0000-0000-0000-000000000001'::uuid, 'berlin-unter-linden', '40000000-0000-0000-0000-000000000001'::uuid, '40000000-0000-0000-0000-000000000001'::uuid, NOW());

  INSERT INTO evidence.building_name_history (building_id, recorded_at, recorded_by, validity, name) VALUES
    ('50200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Berlin Office Complex');

  INSERT INTO evidence.building_address_history (building_id, recorded_at, recorded_by, validity, street, city, postcode, country) VALUES
    ('50200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Unter den Linden 77', 'Berlin', '10117', 'DE');

  INSERT INTO evidence.building_type_history (building_id, recorded_at, recorded_by, validity, building_type_code) VALUES
    ('50200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'office');

  INSERT INTO evidence.building_location_history (building_id, recorded_at, recorded_by, validity, latitude, longitude, anonymization) VALUES
    ('50200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 52.516667, 13.388889, 'street');

  INSERT INTO evidence.building_years_history (building_id, recorded_at, recorded_by, validity, year_built, year_renovated) VALUES
    ('50200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 1995, NULL);

  -- Brno Building
  INSERT INTO evidence.buildings ("Id", uri_slug, owner_id, created_by, created_at) VALUES
    ('50300000-0000-0000-0000-000000000001'::uuid, 'brno-zerotinovo-nam', '30000000-0000-0000-0000-000000000001'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW());

  INSERT INTO evidence.building_name_history (building_id, recorded_at, recorded_by, validity, name) VALUES
    ('50300000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Brno Educational Center');

  INSERT INTO evidence.building_address_history (building_id, recorded_at, recorded_by, validity, street, city, postcode, country) VALUES
    ('50300000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Žerotínovo nám. 9', 'Brno', '60200', 'CZ');

  INSERT INTO evidence.building_type_history (building_id, recorded_at, recorded_by, validity, building_type_code) VALUES
    ('50300000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'educational');

  INSERT INTO evidence.building_location_history (building_id, recorded_at, recorded_by, validity, latitude, longitude, anonymization) VALUES
    ('50300000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 49.195278, 16.608056, 'municipality');

  INSERT INTO evidence.building_years_history (building_id, recorded_at, recorded_by, validity, year_built, year_renovated) VALUES
    ('50300000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 1972, 2012);

  -- ============================================================================
  -- Rooms (10 total)
  -- ============================================================================

  -- Prague Rooms (4)
  INSERT INTO evidence.rooms ("Id", uri_slug, building_id, created_by, created_at) VALUES
    ('60100000-0000-0000-0000-000000000001'::uuid, 'prague-classroom-201', '50100000-0000-0000-0000-000000000001'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW()),
    ('60100000-0000-0000-0000-000000000002'::uuid, 'prague-conference-202', '50100000-0000-0000-0000-000000000001'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW()),
    ('60100000-0000-0000-0000-000000000003'::uuid, 'prague-lab-301', '50100000-0000-0000-0000-000000000001'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW()),
    ('60100000-0000-0000-0000-000000000004'::uuid, 'prague-corridor-2', '50100000-0000-0000-0000-000000000001'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW());

  -- Berlin Rooms (3)
  INSERT INTO evidence.rooms ("Id", uri_slug, building_id, created_by, created_at) VALUES
    ('60200000-0000-0000-0000-000000000001'::uuid, 'berlin-open-office-501', '50200000-0000-0000-0000-000000000001'::uuid, '40000000-0000-0000-0000-000000000001'::uuid, NOW()),
    ('60200000-0000-0000-0000-000000000002'::uuid, 'berlin-meeting-502', '50200000-0000-0000-0000-000000000001'::uuid, '40000000-0000-0000-0000-000000000001'::uuid, NOW()),
    ('60200000-0000-0000-0000-000000000003'::uuid, 'berlin-kitchen-503', '50200000-0000-0000-0000-000000000001'::uuid, '40000000-0000-0000-0000-000000000001'::uuid, NOW());

  -- Brno Rooms (3)
  INSERT INTO evidence.rooms ("Id", uri_slug, building_id, created_by, created_at) VALUES
    ('60300000-0000-0000-0000-000000000001'::uuid, 'brno-classroom-101', '50300000-0000-0000-0000-000000000001'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW()),
    ('60300000-0000-0000-0000-000000000002'::uuid, 'brno-office-201', '50300000-0000-0000-0000-000000000001'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW()),
    ('60300000-0000-0000-0000-000000000003'::uuid, 'brno-storage-b1', '50300000-0000-0000-0000-000000000001'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW());

  -- Room attributes (Prague)
  INSERT INTO evidence.room_name_history (room_id, recorded_at, recorded_by, validity, name) VALUES
    ('60100000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Classroom 201'),
    ('60100000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Conference Room 202'),
    ('60100000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Lab 301'),
    ('60100000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Corridor 2');

  INSERT INTO evidence.room_floor_history (room_id, recorded_at, recorded_by, validity, floor) VALUES
    ('60100000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 2),
    ('60100000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 2),
    ('60100000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 3),
    ('60100000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 2);

  INSERT INTO evidence.room_function_history (room_id, recorded_at, recorded_by, validity, function_code) VALUES
    ('60100000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'classroom'),
    ('60100000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'conference'),
    ('60100000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'lab'),
    ('60100000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'corridor');

  INSERT INTO evidence.room_exposure_history (room_id, recorded_at, recorded_by, validity, exposure_code) VALUES
    ('60100000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'long'),
    ('60100000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'medium'),
    ('60100000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'long'),
    ('60100000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'short');

  INSERT INTO evidence.room_geometry_history (room_id, recorded_at, recorded_by, validity, area_m2, ceiling_height_m) VALUES
    ('60100000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 60, 3.2),
    ('60100000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 45, 3.0),
    ('60100000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 80, 3.5),
    ('60100000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 15, 3.0);

  INSERT INTO evidence.room_pollution_source_history (room_id, recorded_at, recorded_by, validity, source_code) VALUES
    ('60100000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'none'),
    ('60100000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'none'),
    ('60100000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'chemicals'),
    ('60100000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'none');

  -- Room attributes (Berlin)
  INSERT INTO evidence.room_name_history (room_id, recorded_at, recorded_by, validity, name) VALUES
    ('60200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Open Office 501'),
    ('60200000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Meeting Room 502'),
    ('60200000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Kitchen 503');

  INSERT INTO evidence.room_floor_history (room_id, recorded_at, recorded_by, validity, floor) VALUES
    ('60200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 5),
    ('60200000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 5),
    ('60200000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 5);

  INSERT INTO evidence.room_function_history (room_id, recorded_at, recorded_by, validity, function_code) VALUES
    ('60200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'office'),
    ('60200000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'conference'),
    ('60200000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'kitchen');

  INSERT INTO evidence.room_exposure_history (room_id, recorded_at, recorded_by, validity, exposure_code) VALUES
    ('60200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'long'),
    ('60200000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'medium'),
    ('60200000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'short');

  INSERT INTO evidence.room_geometry_history (room_id, recorded_at, recorded_by, validity, area_m2, ceiling_height_m) VALUES
    ('60200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 120, 3.0),
    ('60200000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 35, 2.8),
    ('60200000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 20, 2.6);

  INSERT INTO evidence.room_ventilation_history (room_id, recorded_at, recorded_by, validity, ventilation_type) VALUES
    ('60200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'mechanical'),
    ('60200000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'mechanical'),
    ('60200000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'hybrid');

  INSERT INTO evidence.room_pollution_source_history (room_id, recorded_at, recorded_by, validity, source_code) VALUES
    ('60200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'none'),
    ('60200000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'none'),
    ('60200000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'cooking');

  -- Room attributes (Brno)
  INSERT INTO evidence.room_name_history (room_id, recorded_at, recorded_by, validity, name) VALUES
    ('60300000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Classroom 101'),
    ('60300000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Office 201'),
    ('60300000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Storage B1');

  INSERT INTO evidence.room_floor_history (room_id, recorded_at, recorded_by, validity, floor) VALUES
    ('60300000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 1),
    ('60300000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 2),
    ('60300000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 0);

  INSERT INTO evidence.room_function_history (room_id, recorded_at, recorded_by, validity, function_code) VALUES
    ('60300000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'classroom'),
    ('60300000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'office'),
    ('60300000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'storage');

  INSERT INTO evidence.room_exposure_history (room_id, recorded_at, recorded_by, validity, exposure_code) VALUES
    ('60300000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'long'),
    ('60300000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'long'),
    ('60300000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'short');

  INSERT INTO evidence.room_geometry_history (room_id, recorded_at, recorded_by, validity, area_m2, ceiling_height_m) VALUES
    ('60300000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 55, 3.2),
    ('60300000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 30, 3.0),
    ('60300000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 25, 2.5);

  INSERT INTO evidence.room_pollution_source_history (room_id, recorded_at, recorded_by, validity, source_code) VALUES
    ('60300000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'none'),
    ('60300000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'none'),
    ('60300000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'none');

  -- ============================================================================
  -- Sensors (15 total)
  -- ============================================================================

  -- Prague Sensors (5)
  INSERT INTO evidence.sensors ("Id", uri_slug, current_building_id, current_room_id, created_by, created_at, api_key_hash) VALUES
    -- IAQ sensor in classroom
    ('70100000-0000-0000-0000-000000000001'::uuid, 'prague-classroom-201-iaq', '50100000-0000-0000-0000-000000000001'::uuid, '60100000-0000-0000-0000-000000000001'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW(), 'de64f9e4d3cdf3eb7d0d4edfbf4266b2c52d21da6baced5b1442a14788c3ec69'),
    -- IAQ sensor in conference
    ('70100000-0000-0000-0000-000000000002'::uuid, 'prague-conference-202-iaq', '50100000-0000-0000-0000-000000000001'::uuid, '60100000-0000-0000-0000-000000000002'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW(), 'c3be7f60d18505588e6e06847761780b1004c1d135786b4db6d1d64fd445e122'),
    -- Multi-parameter sensor in lab
    ('70100000-0000-0000-0000-000000000003'::uuid, 'prague-lab-301-multi', '50100000-0000-0000-0000-000000000001'::uuid, '60100000-0000-0000-0000-000000000003'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW(), 'd62c8ca70e28e6981d51e94602b4aa411625e8672b46b8efd8b313298b551f6b'),
    -- Climate sensor in corridor
    ('70100000-0000-0000-0000-000000000004'::uuid, 'prague-corridor-2-climate', '50100000-0000-0000-0000-000000000001'::uuid, '60100000-0000-0000-0000-000000000004'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW(), '18eb509321c0a773d1a2aaf3ce77a790e1ae1a3398202368788230dab4fa1658'),
    -- Multi-gas sensor (extra for parameter coverage)
    ('70100000-0000-0000-0000-000000000005'::uuid, 'prague-lab-301-multigas', '50100000-0000-0000-0000-000000000001'::uuid, '60100000-0000-0000-0000-000000000003'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW(), 'f024b890cf06b531dd2af4114591fd8be580ec14d33608da18d1353aa1676bdf');

  -- Berlin Sensors (5)
  INSERT INTO evidence.sensors ("Id", uri_slug, current_building_id, current_room_id, created_by, created_at, api_key_hash) VALUES
    -- IAQ sensor in open office
    ('70200000-0000-0000-0000-000000000001'::uuid, 'berlin-office-501-iaq', '50200000-0000-0000-0000-000000000001'::uuid, '60200000-0000-0000-0000-000000000001'::uuid, '40000000-0000-0000-0000-000000000001'::uuid, NOW(), '4fdf1c81e03589c08ddb3cd215023662873a53a49a6c7237899d3f83587604f0'),
    -- IAQ + light sensor in meeting
    ('70200000-0000-0000-0000-000000000002'::uuid, 'berlin-meeting-502-iaq-light', '50200000-0000-0000-0000-000000000001'::uuid, '60200000-0000-0000-0000-000000000002'::uuid, '40000000-0000-0000-0000-000000000001'::uuid, NOW(), '4f7bb687ae90c7e759c2e8182e84156f31394d5b48b1efc08f1d3f2fdcf0a951'),
    -- Particulate + acoustic sensor in kitchen
    ('70200000-0000-0000-0000-000000000003'::uuid, 'berlin-kitchen-503-multi', '50200000-0000-0000-0000-000000000001'::uuid, '60200000-0000-0000-0000-000000000003'::uuid, '40000000-0000-0000-0000-000000000001'::uuid, NOW(), 'fcbcdabb36e38177350a4e1460776ce033323647eda43b2566b715b56c4becc2'),
    -- Light sensor (extra for coverage)
    ('70200000-0000-0000-0000-000000000004'::uuid, 'berlin-meeting-502-light', '50200000-0000-0000-0000-000000000001'::uuid, '60200000-0000-0000-0000-000000000002'::uuid, '40000000-0000-0000-0000-000000000001'::uuid, NOW(), '87eaa08d639f833699b4e6bfc26c4dbc0941b5ab2622d70bbb670a050c70aca3'),
    -- Acoustic sensor (extra)
    ('70200000-0000-0000-0000-000000000005'::uuid, 'berlin-kitchen-503-acoustic', '50200000-0000-0000-0000-000000000001'::uuid, '60200000-0000-0000-0000-000000000003'::uuid, '40000000-0000-0000-0000-000000000001'::uuid, NOW(), '2b96d6faad058da07733b292fa6728737792aa1e0515974a2527e3bfed68d1f5');

  -- Brno Sensors (5)
  INSERT INTO evidence.sensors ("Id", uri_slug, current_building_id, current_room_id, created_by, created_at, api_key_hash) VALUES
    -- IAQ sensor in classroom
    ('70300000-0000-0000-0000-000000000001'::uuid, 'brno-classroom-101-iaq', '50300000-0000-0000-0000-000000000001'::uuid, '60300000-0000-0000-0000-000000000001'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW(), '895fa1f44eaa707132914420162b09eb0f7e0236c11e5c661a453b2e7f8d9068'),
    -- Climate sensor in office
    ('70300000-0000-0000-0000-000000000002'::uuid, 'brno-office-201-climate', '50300000-0000-0000-0000-000000000001'::uuid, '60300000-0000-0000-0000-000000000002'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW(), '5590d0fd6ff8825ff9f7fd5ab40d9952ea25d5189e37ab90789c18736de6704d'),
    -- Particulate sensor in storage
    ('70300000-0000-0000-0000-000000000003'::uuid, 'brno-storage-b1-pm', '50300000-0000-0000-0000-000000000001'::uuid, '60300000-0000-0000-0000-000000000003'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW(), 'fc253df3fb1838895d6f59eadb884daadb95a0764e1d4a7b55034d17b07e81a4'),
    -- Light sensor in classroom (extra coverage)
    ('70300000-0000-0000-0000-000000000004'::uuid, 'prague-classroom-201-light', '50100000-0000-0000-0000-000000000001'::uuid, '60100000-0000-0000-0000-000000000001'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW(), '1f301c0a55660af48f7cb8cb199ba1f3deec953716660039a1815ece4733ddb5'),
    -- Acoustic sensor in Prague conference (extra)
    ('70300000-0000-0000-0000-000000000005'::uuid, 'prague-conference-202-acoustic', '50100000-0000-0000-0000-000000000001'::uuid, '60100000-0000-0000-0000-000000000002'::uuid, '30000000-0000-0000-0000-000000000001'::uuid, NOW(), 'beab478a5483799cf833a5ee7bdcf4e172a6b864774a9b50c6f84c3062acbe85');

  -- Sensor Identity History (all 15)
  INSERT INTO evidence.sensor_identity_history (sensor_id, recorded_at, recorded_by, validity, manufacturer, model, serial_number) VALUES
    ('70100000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Sensirion', 'SCD40', 'SCD40-001'),
    ('70100000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Sensirion', 'SCD40', 'SCD40-002'),
    ('70100000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Plantower', 'PMS5003', 'PMS5003-001'),
    ('70100000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Sensirion', 'SDP810', 'SDP810-001'),
    ('70100000-0000-0000-0000-000000000005'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Bosch', 'BME688', 'BME688-001'),
    ('70200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Sensirion', 'SCD40', 'SCD40-003'),
    ('70200000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Sensirion', 'SCD40', 'SCD40-004'),
    ('70200000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Plantower', 'PMS5003', 'PMS5003-002'),
    ('70200000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Texas Instruments', 'OPT3001', 'OPT3001-001'),
    ('70200000-0000-0000-0000-000000000005'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'MEMS', 'SoundLevel', 'SOUND-001'),
    ('70300000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Sensirion', 'SCD40', 'SCD40-005'),
    ('70300000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Sensirion', 'SDP810', 'SDP810-002'),
    ('70300000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Plantower', 'PMS5003', 'PMS5003-003'),
    ('70300000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'Texas Instruments', 'OPT3001', 'OPT3001-002'),
    ('70300000-0000-0000-0000-000000000005'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'MEMS', 'SoundLevel', 'SOUND-002');

  -- Sensor Placement History (all 15)
  INSERT INTO evidence.sensor_placement_history (sensor_id, recorded_at, recorded_by, validity, building_id, room_id) VALUES
    ('70100000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50100000-0000-0000-0000-000000000001'::uuid, '60100000-0000-0000-0000-000000000001'::uuid),
    ('70100000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50100000-0000-0000-0000-000000000001'::uuid, '60100000-0000-0000-0000-000000000002'::uuid),
    ('70100000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50100000-0000-0000-0000-000000000001'::uuid, '60100000-0000-0000-0000-000000000003'::uuid),
    ('70100000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50100000-0000-0000-0000-000000000001'::uuid, '60100000-0000-0000-0000-000000000004'::uuid),
    ('70100000-0000-0000-0000-000000000005'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50100000-0000-0000-0000-000000000001'::uuid, '60100000-0000-0000-0000-000000000003'::uuid),
    ('70200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50200000-0000-0000-0000-000000000001'::uuid, '60200000-0000-0000-0000-000000000001'::uuid),
    ('70200000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50200000-0000-0000-0000-000000000001'::uuid, '60200000-0000-0000-0000-000000000002'::uuid),
    ('70200000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50200000-0000-0000-0000-000000000001'::uuid, '60200000-0000-0000-0000-000000000003'::uuid),
    ('70200000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50200000-0000-0000-0000-000000000001'::uuid, '60200000-0000-0000-0000-000000000002'::uuid),
    ('70200000-0000-0000-0000-000000000005'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50200000-0000-0000-0000-000000000001'::uuid, '60200000-0000-0000-0000-000000000003'::uuid),
    ('70300000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50300000-0000-0000-0000-000000000001'::uuid, '60300000-0000-0000-0000-000000000001'::uuid),
    ('70300000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50300000-0000-0000-0000-000000000001'::uuid, '60300000-0000-0000-0000-000000000002'::uuid),
    ('70300000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50300000-0000-0000-0000-000000000001'::uuid, '60300000-0000-0000-0000-000000000003'::uuid),
    ('70300000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50100000-0000-0000-0000-000000000001'::uuid, '60100000-0000-0000-0000-000000000001'::uuid),
    ('70300000-0000-0000-0000-000000000005'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), '50100000-0000-0000-0000-000000000001'::uuid, '60100000-0000-0000-0000-000000000002'::uuid);

  -- Sensor Status History (all 15)
  INSERT INTO evidence.sensor_status_history (sensor_id, recorded_at, recorded_by, validity, status_code) VALUES
    ('70100000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active'),
    ('70100000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active'),
    ('70100000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active'),
    ('70100000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active'),
    ('70100000-0000-0000-0000-000000000005'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active'),
    ('70200000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active'),
    ('70200000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active'),
    ('70200000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active'),
    ('70200000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active'),
    ('70200000-0000-0000-0000-000000000005'::uuid, '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active'),
    ('70300000-0000-0000-0000-000000000001'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active'),
    ('70300000-0000-0000-0000-000000000002'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active'),
    ('70300000-0000-0000-0000-000000000003'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active'),
    ('70300000-0000-0000-0000-000000000004'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active'),
    ('70300000-0000-0000-0000-000000000005'::uuid, '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'), 'active');

  -- Sensor Measured Parameters (one row per (sensor, parameter) pair)
  INSERT INTO evidence.sensor_measured_parameter_history (sensor_id, parameter_code, recorded_at, recorded_by, validity) VALUES
    -- Prague sensors: classroom IAQ
    ('70100000-0000-0000-0000-000000000001'::uuid, 'co2', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70100000-0000-0000-0000-000000000001'::uuid, 'temperature', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70100000-0000-0000-0000-000000000001'::uuid, 'humidity', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    -- Prague sensors: conference IAQ + acoustic
    ('70100000-0000-0000-0000-000000000002'::uuid, 'co2', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70100000-0000-0000-0000-000000000002'::uuid, 'temperature', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70100000-0000-0000-0000-000000000002'::uuid, 'humidity', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    -- Prague sensors: lab particulates
    ('70100000-0000-0000-0000-000000000003'::uuid, 'pm1', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70100000-0000-0000-0000-000000000003'::uuid, 'pm2_5', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70100000-0000-0000-0000-000000000003'::uuid, 'pm4', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70100000-0000-0000-0000-000000000003'::uuid, 'pm10', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    -- Prague sensors: corridor climate
    ('70100000-0000-0000-0000-000000000004'::uuid, 'air_velocity', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70100000-0000-0000-0000-000000000004'::uuid, 'pressure', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    -- Prague sensors: multi-gas (lab)
    ('70100000-0000-0000-0000-000000000005'::uuid, 'eco2', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70100000-0000-0000-0000-000000000005'::uuid, 'co', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70100000-0000-0000-0000-000000000005'::uuid, 'o3', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70100000-0000-0000-0000-000000000005'::uuid, 'no2', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70100000-0000-0000-0000-000000000005'::uuid, 'so2', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70100000-0000-0000-0000-000000000005'::uuid, 'voc', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    -- Berlin sensors: open office IAQ
    ('70200000-0000-0000-0000-000000000001'::uuid, 'co2', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70200000-0000-0000-0000-000000000001'::uuid, 'temperature', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70200000-0000-0000-0000-000000000001'::uuid, 'humidity', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    -- Berlin sensors: meeting IAQ + light
    ('70200000-0000-0000-0000-000000000002'::uuid, 'co2', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70200000-0000-0000-0000-000000000002'::uuid, 'temperature', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70200000-0000-0000-0000-000000000002'::uuid, 'humidity', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70200000-0000-0000-0000-000000000002'::uuid, 'illuminance', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70200000-0000-0000-0000-000000000002'::uuid, 'cct', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    -- Berlin sensors: kitchen particulates + acoustic
    ('70200000-0000-0000-0000-000000000003'::uuid, 'pm1', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70200000-0000-0000-0000-000000000003'::uuid, 'pm2_5', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70200000-0000-0000-0000-000000000003'::uuid, 'pm4', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70200000-0000-0000-0000-000000000003'::uuid, 'pm10', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70200000-0000-0000-0000-000000000003'::uuid, 'laeq', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    -- Berlin sensors: extra light
    ('70200000-0000-0000-0000-000000000004'::uuid, 'illuminance', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70200000-0000-0000-0000-000000000004'::uuid, 'cct', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    -- Berlin sensors: extra acoustic
    ('70200000-0000-0000-0000-000000000005'::uuid, 'laeq', '2025-01-01'::timestamptz, '40000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    -- Brno sensors: classroom IAQ
    ('70300000-0000-0000-0000-000000000001'::uuid, 'co2', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70300000-0000-0000-0000-000000000001'::uuid, 'temperature', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70300000-0000-0000-0000-000000000001'::uuid, 'humidity', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    -- Brno sensors: office climate
    ('70300000-0000-0000-0000-000000000002'::uuid, 'air_velocity', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70300000-0000-0000-0000-000000000002'::uuid, 'pressure', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    -- Brno sensors: storage particulates
    ('70300000-0000-0000-0000-000000000003'::uuid, 'pm1', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70300000-0000-0000-0000-000000000003'::uuid, 'pm2_5', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70300000-0000-0000-0000-000000000003'::uuid, 'pm4', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70300000-0000-0000-0000-000000000003'::uuid, 'pm10', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    -- Extra: Prague classroom light
    ('70300000-0000-0000-0000-000000000004'::uuid, 'illuminance', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    ('70300000-0000-0000-0000-000000000004'::uuid, 'cct', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)')),
    -- Extra: Prague conference acoustic
    ('70300000-0000-0000-0000-000000000005'::uuid, 'laeq', '2025-01-01'::timestamptz, '30000000-0000-0000-0000-000000000001'::uuid, tstzrange('2025-01-01'::timestamptz, NULL, '[)'));

  RAISE NOTICE 'Evidence seed applied: 2 user_projections, 3 buildings, 10 rooms, 15 sensors.';
END $$;
