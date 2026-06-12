// Public read API load test (NFRs: p95 < 1 s and p99 < 3 s for pages of
// <= 100 records, under >= 50 concurrent requests).
//
// 50 constant VUs issue a mix of catalog and observation reads, each page
// capped at 100 records. Thresholds encode the NFRs directly, so a failed
// run = a violated requirement.
//
//   k6 run k6/public-read.js
//   k6 run -e VUS=50 -e DURATION=5m --summary-export k6/results/public-read.json k6/public-read.js
import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';

export const options = {
  scenarios: {
    read: {
      executor: 'constant-vus',
      vus: Number(__ENV.VUS || 50),
      duration: __ENV.DURATION || '2m',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<1000', 'p(99)<3000'],
    http_req_failed: ['rate<0.01'],
  },
};

const targets = [
  `${BASE_URL}/public/v1/observations/?limit=100`,
  `${BASE_URL}/public/v1/observations/?parameterCode=co2&limit=100`,
  `${BASE_URL}/public/v1/buildings`,
  `${BASE_URL}/public/v1/codelists`,
];

export default function () {
  const url = targets[Math.floor(Math.random() * targets.length)];
  const res = http.get(url);
  check(res, { 'status 200': (r) => r.status === 200 });
}
