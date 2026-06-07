-- Seed data for auth database (development only)
-- Users: alice@ambiquality.dev and bob@ambiquality.dev
-- Password for both: Ambiquality2025!

DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM auth.users WHERE email = 'alice@ambiquality.dev') THEN
    RAISE NOTICE 'Auth seed already applied, skipping.';
    RETURN;
  END IF;

  -- Alice - Prague + Brno
  INSERT INTO auth.users (
    "Id", email, email_confirmed, password_hash, failed_login_count
  ) VALUES (
    '10000000-0000-0000-0000-000000000001'::uuid,
    'alice@ambiquality.dev',
    true,
    'AQAAAAMAAzRQAAAAEE8R7aDcU8Ob7OjXxAhLeF7yIdagnP6lgfxObjQRo2Dw0sKQzgkUkKP9F1sUHeK/9g==',
    0
  );

  -- Bob - Berlin
  INSERT INTO auth.users (
    "Id", email, email_confirmed, password_hash, failed_login_count
  ) VALUES (
    '20000000-0000-0000-0000-000000000001'::uuid,
    'bob@ambiquality.dev',
    true,
    'AQAAAAMAAzRQAAAAEE8R7aDcU8Ob7OjXxAhLeF7yIdagnP6lgfxObjQRo2Dw0sKQzgkUkKP9F1sUHeK/9g==',
    0
  );

  RAISE NOTICE 'Auth seed applied: 2 users created.';
END $$;
