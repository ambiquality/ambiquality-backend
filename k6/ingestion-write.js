// Ingestion throughput test (NFR: >= 100 measurements/s sustained).
//
// Drives POST /ingestion/v1/measurements at a constant arrival rate; each
// request carries one reading, so RATE requests/s == RATE measurements/s.
// The API ack is 202 (durably enqueued) — this measures the accept path;
// the worker drains the Redis stream asynchronously.
//
//   ./k6/seed.sh
//   k6 run k6/ingestion-write.js                       # 100 msg/s for 2m
//   k6 run -e RATE=200 -e DURATION=5m k6/ingestion-write.js
//   k6 run --summary-export k6/results/ingestion.json k6/ingestion-write.js
import http from 'k6/http';
import { check } from 'k6';

const seed = JSON.parse(open('./seed.json'));
const BASE_URL = __ENV.BASE_URL || seed.baseUrl;
const RATE = Number(__ENV.RATE || 100);

export const options = {
  scenarios: {
    ingest: {
      executor: 'constant-arrival-rate',
      rate: RATE,
      timeUnit: '1s',
      duration: __ENV.DURATION || '2m',
      preAllocatedVUs: 50,
      maxVUs: 200,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
    http_req_duration: ['p(95)<1000'],
  },
};

const params = {
  headers: {
    'Content-Type': 'application/json',
    'X-Sensor-Key': seed.sensorApiKey,
  },
};

export default function () {
  const body = JSON.stringify({
    sensorId: seed.sensorId,
    readings: [
      { parameterCode: 'co2', value: 400 + Math.random() * 1600, unit: 'ppm' },
    ],
  });
  const res = http.post(`${BASE_URL}/ingestion/v1/measurements`, body, params);
  check(res, { 'status 202': (r) => r.status === 202 });
}
