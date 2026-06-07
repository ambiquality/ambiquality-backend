-- Seed data for ieq database (development only)
-- Inserts ~120 measurements per (sensor, parameter) pair
-- Generated via generate_series: 15 days × 8 samples/day (every 3 hours)

DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM ieq.measurements LIMIT 1) THEN
    RAISE NOTICE 'IEQ seed already applied, skipping.';
    RETURN;
  END IF;

  -- ============================================================================
  -- Prague Sensors - Classroom IAQ (CO2, temperature, humidity)
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000001'::uuid,
    'co2',
    (400 + (random() * 900))::double precision,
    'ppm',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000001'::uuid,
    'temperature',
    (19 + (random() * 6))::double precision,
    '°C',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000001'::uuid,
    'humidity',
    (35 + (random() * 30))::double precision,
    '%',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  -- ============================================================================
  -- Prague Sensors - Conference IAQ
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000002'::uuid,
    'co2',
    (400 + (random() * 900))::double precision,
    'ppm',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000002'::uuid,
    'temperature',
    (19 + (random() * 6))::double precision,
    '°C',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000002'::uuid,
    'humidity',
    (35 + (random() * 30))::double precision,
    '%',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  -- ============================================================================
  -- Prague Sensors - Lab Particulates
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000003'::uuid,
    'pm1',
    (3 + (random() * 20))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000003'::uuid,
    'pm2_5',
    (5 + (random() * 35))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000003'::uuid,
    'pm4',
    (8 + (random() * 50))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000003'::uuid,
    'pm10',
    (10 + (random() * 70))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  -- ============================================================================
  -- Prague Sensors - Corridor Climate
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000004'::uuid,
    'air_velocity',
    (0.05 + (random() * 0.2))::double precision,
    'm/s',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000004'::uuid,
    'pressure',
    (100500 + (random() * 800))::double precision,
    'Pa',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  -- ============================================================================
  -- Prague Sensors - Lab Multi-Gas
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000005'::uuid,
    'eco2',
    (450 + (random() * 1200))::double precision,
    'ppm',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000005'::uuid,
    'co',
    (0.5 + (random() * 3))::double precision,
    'ppm',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000005'::uuid,
    'o3',
    (15 + (random() * 40))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000005'::uuid,
    'no2',
    (10 + (random() * 25))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000005'::uuid,
    'so2',
    (2 + (random() * 10))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70100000-0000-0000-0000-000000000005'::uuid,
    'voc',
    (150 + (random() * 300))::double precision,
    'ppb',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  -- ============================================================================
  -- Berlin Sensors - Open Office IAQ
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000001'::uuid,
    'co2',
    (400 + (random() * 900))::double precision,
    'ppm',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000001'::uuid,
    'temperature',
    (19 + (random() * 6))::double precision,
    '°C',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000001'::uuid,
    'humidity',
    (35 + (random() * 30))::double precision,
    '%',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  -- ============================================================================
  -- Berlin Sensors - Meeting IAQ + Light
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000002'::uuid,
    'co2',
    (400 + (random() * 900))::double precision,
    'ppm',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000002'::uuid,
    'temperature',
    (19 + (random() * 6))::double precision,
    '°C',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000002'::uuid,
    'humidity',
    (35 + (random() * 30))::double precision,
    '%',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000002'::uuid,
    'illuminance',
    (300 + (random() * 400))::double precision,
    'lx',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000002'::uuid,
    'cct',
    (3500 + (random() * 1000))::double precision,
    'K',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  -- ============================================================================
  -- Berlin Sensors - Kitchen Particulates + Acoustic
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000003'::uuid,
    'pm1',
    (3 + (random() * 20))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000003'::uuid,
    'pm2_5',
    (5 + (random() * 35))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000003'::uuid,
    'pm4',
    (8 + (random() * 50))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000003'::uuid,
    'pm10',
    (10 + (random() * 70))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000003'::uuid,
    'laeq',
    (32 + (random() * 25))::double precision,
    'dB(A)',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  -- ============================================================================
  -- Berlin Sensors - Extra Light (Meeting)
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000004'::uuid,
    'illuminance',
    (300 + (random() * 400))::double precision,
    'lx',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000004'::uuid,
    'cct',
    (3500 + (random() * 1000))::double precision,
    'K',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  -- ============================================================================
  -- Berlin Sensors - Extra Acoustic (Kitchen)
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70200000-0000-0000-0000-000000000005'::uuid,
    'laeq',
    (32 + (random() * 25))::double precision,
    'dB(A)',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  -- ============================================================================
  -- Brno Sensors - Classroom IAQ
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70300000-0000-0000-0000-000000000001'::uuid,
    'co2',
    (400 + (random() * 900))::double precision,
    'ppm',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70300000-0000-0000-0000-000000000001'::uuid,
    'temperature',
    (19 + (random() * 6))::double precision,
    '°C',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70300000-0000-0000-0000-000000000001'::uuid,
    'humidity',
    (35 + (random() * 30))::double precision,
    '%',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  -- ============================================================================
  -- Brno Sensors - Office Climate
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70300000-0000-0000-0000-000000000002'::uuid,
    'air_velocity',
    (0.05 + (random() * 0.2))::double precision,
    'm/s',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70300000-0000-0000-0000-000000000002'::uuid,
    'pressure',
    (100500 + (random() * 800))::double precision,
    'Pa',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  -- ============================================================================
  -- Brno Sensors - Storage Particulates
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70300000-0000-0000-0000-000000000003'::uuid,
    'pm1',
    (3 + (random() * 20))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70300000-0000-0000-0000-000000000003'::uuid,
    'pm2_5',
    (5 + (random() * 35))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70300000-0000-0000-0000-000000000003'::uuid,
    'pm4',
    (8 + (random() * 50))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70300000-0000-0000-0000-000000000003'::uuid,
    'pm10',
    (10 + (random() * 70))::double precision,
    'µg/m³',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  -- ============================================================================
  -- Extra Sensors - Prague Classroom Light
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70300000-0000-0000-0000-000000000004'::uuid,
    'illuminance',
    (300 + (random() * 400))::double precision,
    'lx',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70300000-0000-0000-0000-000000000004'::uuid,
    'cct',
    (3500 + (random() * 1000))::double precision,
    'K',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  -- ============================================================================
  -- Extra Sensors - Prague Conference Acoustic
  -- ============================================================================

  INSERT INTO ieq.measurements (id, received_at, sensor_id, parameter_code, value, unit, observed_at, is_invalid)
  SELECT
    gen_random_uuid(),
    ts,
    '70300000-0000-0000-0000-000000000005'::uuid,
    'laeq',
    (32 + (random() * 25))::double precision,
    'dB(A)',
    ts - (random() * INTERVAL '5 seconds'),
    false
  FROM generate_series(NOW() - INTERVAL '15 days', NOW(), INTERVAL '3 hours') AS ts;

  RAISE NOTICE 'IEQ seed applied: ~1800 measurements (120 per sensor-parameter pair, 15 sensors × multi-parameter).';
END $$;
