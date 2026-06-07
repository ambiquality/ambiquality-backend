# Development Seed Data — Test Credentials & API Keys

This file documents the seed data automatically populated when running `./dev.sh up --profile development`.

## Test User Credentials

Both users are pre-confirmed (email_confirmed = true) and ready to log in immediately.

```
Email: alice@ambiquality.dev
Password: Ambiquality2025!
Owns: Prague (precise anonymization) and Brno (municipality anonymization) buildings

Email: bob@ambiquality.dev
Password: Ambiquality2025!
Owns: Berlin (street-level anonymization) building
```

## Sensor API Keys (Plaintext → SHA-256)

These are documented for reference. **Do NOT commit plaintext keys to version control** — they are stored only in the seed SQL as pre-hashed values.

```
Plaintext API Key                          →  SHA-256 Hash
─────────────────────────────────────────────────────────────────────────────
seed-sensor-prague-classroom-201           →  de64f9e4d3cdf3eb7d0d4edfbf4266b2c52d21da6baced5b1442a14788c3ec69
seed-sensor-prague-conference-202          →  c3be7f60d18505588e6e06847761780b1004c1d135786b4db6d1d64fd445e122
seed-sensor-prague-lab-301                 →  d62c8ca70e28e6981d51e94602b4aa411625e8672b46b8efd8b313298b551f6b
seed-sensor-prague-corridor-2              →  18eb509321c0a773d1a2aaf3ce77a790e1ae1a3398202368788230dab4fa1658
seed-sensor-berlin-open-office-501         →  4fdf1c81e03589c08ddb3cd215023662873a53a49a6c7237899d3f83587604f0
seed-sensor-berlin-meeting-502             →  4f7bb687ae90c7e759c2e8182e84156f31394d5b48b1efc08f1d3f2fdcf0a951
seed-sensor-berlin-kitchen-503             →  fcbcdabb36e38177350a4e1460776ce033323647eda43b2566b715b56c4becc2
seed-sensor-brno-classroom-101             →  895fa1f44eaa707132914420162b09eb0f7e0236c11e5c661a453b2e7f8d9068
seed-sensor-brno-office-201                →  5590d0fd6ff8825ff9f7fd5ab40d9952ea25d5189e37ab90789c18736de6704d
seed-sensor-brno-storage-b1                →  fc253df3fb1838895d6f59eadb884daadb95a0764e1d4a7b55034d17b07e81a4
seed-sensor-prague-multi-gas               →  f024b890cf06b531dd2af4114591fd8be580ec14d33608da18d1353aa1676bdf
seed-sensor-prague-light-1                 →  1f301c0a55660af48f7cb8cb199ba1f3deec953716660039a1815ece4733ddb5
seed-sensor-berlin-light-2                 →  87eaa08d639f833699b4e6bfc26c4dbc0941b5ab2622d70bbb670a050c70aca3
seed-sensor-prague-acoustic                →  beab478a5483799cf833a5ee7bdcf4e172a6b864774a9b50c6f84c3062acbe85
seed-sensor-berlin-acoustic                →  2b96d6faad058da07733b292fa6728737792aa1e0515974a2527e3bfed68d1f5
```

## Data Summary

### Buildings (3)
- **Prague** (VŠE FIS) — Educational, precise coordinates (50.079167°N, 14.433056°E) — Owner: Alice
- **Berlin** (Unter den Linden 77) — Office, street-level coordinates (52.516667°N, 13.388889°E) — Owner: Bob
- **Brno** (Žerotínovo nám. 9) — Educational, municipality-level anonymization (49.195278°N, 16.608056°E) — Owner: Alice

### Rooms (10)
- **Prague** (4): Classroom 201, Conference 202, Lab 301, Corridor 2
- **Berlin** (3): Open Office 501, Meeting 502, Kitchen 503
- **Brno** (3): Classroom 101, Office 201, Storage B1

### Sensors (15)
Deploy across all rooms, covering all 18 measurement parameters:

| Sensor Type | Parameters | Count | Locations |
|---|---|---|---|
| IAQ (CO₂, Temp, Humidity) | co2, temperature, humidity | 5 | Prague classroom, conference; Berlin office; Brno classroom |
| Particulate (PM) | pm1, pm2_5, pm4, pm10 | 3 | Prague lab, Berlin kitchen, Brno storage |
| Multi-gas | eco2, co, o3, no2, so2, voc | 1 | Prague lab |
| Light | illuminance, cct | 2 | Prague classroom, Berlin meeting |
| Acoustic | laeq | 2 | Prague conference, Berlin kitchen |
| Climate | air_velocity, pressure | 2 | Prague corridor, Brno office |

**All 18 parameters covered at least once.**

### Measurements (~1800 total)
Each sensor-parameter pair has **~120 data points** (15 days of 3-hourly observations):
- Time range: last 15 days from now
- Frequency: every 3 hours
- Values: realistic ranges per parameter code
- Status: all flagged `is_invalid = false`

## Technical Notes

### Password Hashing
Both users use the same password hashed with ASP.NET Core Identity v3 (PBKDF2-SHA512, 210k iterations).

### Idempotency
All three seed SQL files (auth, evidence, ieq) are idempotent — they check for existing data before inserting. Running `./dev.sh up` again with the same volumes **will not duplicate** the seed data.

To reseed with fresh data, explicitly run `./dev.sh down` (which removes all volumes) before the next `./dev.sh up`.

### Foreign Key Constraints
- User UUIDs are fixed and hardcoded in both auth and evidence seeds for stability
- Building/room/sensor IDs form a stable hierarchy
- All measurement records reference valid sensor IDs
- All temporal history rows have open validity ranges (upper bound = ∞)

## Accessing Seeded Data

### Via PgAdmin (if running with development profile)
- URL: http://localhost:5050
- Email/password: configured in `.env`
- Pre-configured servers in `conf/pgadmin/servers.json`

### Via psql (from host)
```bash
# Evidence catalog
psql "postgresql://public_api:public_api_dev_password@localhost:6432/evidence" \
  -c "SELECT * FROM evidence.buildings"

# IEQ observations
psql "postgresql://public_api:public_api_dev_password@localhost:6432/ieq" \
  -c "SELECT COUNT(*), parameter_code FROM ieq.measurements GROUP BY 2"
```

### Via Public API
```bash
curl "http://localhost:8080/public/observations?pageSize=10"
```

### Via Auth API (after login)
```bash
TOKEN=$(curl -s -X POST http://localhost:8080/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"alice@ambiquality.dev","password":"Ambiquality2025!"}' \
  | jq -r '.access_token')

curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/evidence/buildings
```
