#!/usr/bin/env bash
# Seeds the running dev stack with one user → building → room → sensor and
# writes k6/seed.json with the IDs + sensor API key the k6 scenarios need.
# Mirrors the newman E2E flow: the confirmation link is pulled from Mailpit.
#
#   ./dev.sh up-d            # stack must be running
#   ./k6/seed.sh             # then: k6 run k6/ingestion-write.js
#
# Env overrides: BASE_URL (default http://localhost:8080),
#                MAILPIT_URL (default http://localhost:8025)
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:8080}"
MAILPIT_URL="${MAILPIT_URL:-http://localhost:8025}"
HERE="$(cd "$(dirname "$0")" && pwd)"
TS="$(date +%s)"
EMAIL="k6+${TS}@example.com"
PASSWORD="Sup3rSecret!"

say() { echo "[seed] $*" >&2; }

say "register ${EMAIL}"
curl -fsS -o /dev/null -X POST "${BASE_URL}/auth/v1/register" \
  -H 'Content-Type: application/json' \
  -d "{\"email\": \"${EMAIL}\", \"password\": \"${PASSWORD}\"}"

say "waiting for confirmation mail in Mailpit"
MSG_ID=""
for _ in $(seq 1 15); do
  MSG_ID="$(curl -fsS "${MAILPIT_URL}/api/v1/messages" \
    | jq -r --arg to "${EMAIL}" '.messages[] | select((.To // []) | map(.Address) | index($to)) | .ID' \
    | head -1)"
  [ -n "${MSG_ID}" ] && break
  sleep 1
done
[ -n "${MSG_ID}" ] || { say "no confirmation mail for ${EMAIL}"; exit 1; }

BODY="$(curl -fsS "${MAILPIT_URL}/api/v1/message/${MSG_ID}" | jq -r '.Text')"
USER_ID="$(echo "${BODY}" | grep -o 'userId=[^&]*' | head -1 | cut -d= -f2)"
TOKEN="$(echo "${BODY}" | grep -o 'token=[^[:space:]]*' | head -1 | cut -d= -f2)"
[ -n "${USER_ID}" ] && [ -n "${TOKEN}" ] || { say "could not parse confirmation link"; exit 1; }

say "confirm e-mail"
curl -fsS -o /dev/null "${BASE_URL}/auth/v1/confirm-email?userId=${USER_ID}&token=${TOKEN}"

say "login"
ACCESS_TOKEN="$(curl -fsS -X POST "${BASE_URL}/auth/v1/login" \
  -H 'Content-Type: application/json' \
  -d "{\"email\": \"${EMAIL}\", \"password\": \"${PASSWORD}\"}" | jq -r '.accessToken')"

AUTH=(-H "Authorization: Bearer ${ACCESS_TOKEN}" -H 'Content-Type: application/json')

say "create building"
BUILDING_ID="$(curl -fsS -X POST "${BASE_URL}/evidence/v1/buildings/" "${AUTH[@]}" -d @- <<EOF | jq -r '.id'
{
  "name": "k6 Load Test Building ${TS}",
  "addressPointCode": 21794547,
  "streetName": "Žižkova",
  "houseNumber": 11,
  "houseNumberType": "č.p.",
  "orientationNumber": null,
  "orientationNumberLetter": null,
  "municipalityName": "Praha",
  "municipalityPartName": "Žižkov",
  "psc": "13000",
  "districtName": "Hlavní město Praha",
  "regionName": "Hlavní město Praha",
  "municipalityCode": 554782,
  "regionCode": 19,
  "buildingTypeCode": "office",
  "latitude": 50.0815,
  "longitude": 14.4391,
  "yearBuilt": 1962,
  "yearRenovated": 2005
}
EOF
)"

say "create room"
ROOM_ID="$(curl -fsS -X POST "${BASE_URL}/evidence/v1/buildings/${BUILDING_ID}/rooms/" "${AUTH[@]}" -d @- <<EOF | jq -r '.id'
{
  "uriSlug": "k6-room-${TS}",
  "name": "k6 Room ${TS}",
  "floor": 1,
  "functionCode": "office",
  "exposureCode": "short",
  "areaM2": 35.5,
  "ceilingHeightM": 2.8,
  "ventilationType": "mechanical",
  "pollutionSources": ["traffic"]
}
EOF
)"

say "create sensor"
SENSOR_JSON="$(curl -fsS -X POST "${BASE_URL}/evidence/v1/buildings/${BUILDING_ID}/rooms/${ROOM_ID}/sensors/" "${AUTH[@]}" -d @- <<EOF
{
  "uriSlug": "k6-sensor-${TS}",
  "manufacturer": "Aranet",
  "model": "Aranet4",
  "serialNumber": "SN-K6-${TS}",
  "statusCode": "active",
  "measuredParameters": ["co2", "temperature", "humidity"]
}
EOF
)"

jq -n \
  --arg baseUrl "${BASE_URL}" \
  --arg buildingId "${BUILDING_ID}" \
  --arg roomId "${ROOM_ID}" \
  --arg sensorId "$(echo "${SENSOR_JSON}" | jq -r '.id')" \
  --arg sensorApiKey "$(echo "${SENSOR_JSON}" | jq -r '.apiKey')" \
  '{baseUrl: $baseUrl, buildingId: $buildingId, roomId: $roomId, sensorId: $sensorId, sensorApiKey: $sensorApiKey}' \
  > "${HERE}/seed.json"

say "wrote $(realpath "${HERE}/seed.json")"
