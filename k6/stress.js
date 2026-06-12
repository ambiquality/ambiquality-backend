// Exploratory stress test of the public read API: ramps the arrival rate
// well past the NFR level to find the saturation point (the "knee" where
// latency and error rate take off). No pass/fail thresholds — the output
// is the result.
//
//   k6 run --summary-export k6/results/stress.json k6/stress.js
import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';

export const options = {
  scenarios: {
    stress: {
      executor: 'ramping-arrival-rate',
      startRate: 25,
      timeUnit: '1s',
      preAllocatedVUs: 100,
      maxVUs: 500,
      stages: [
        { target: 50, duration: '1m' },
        { target: 100, duration: '2m' },
        { target: 200, duration: '2m' },
        { target: 400, duration: '2m' },
        { target: 0, duration: '1m' },
      ],
    },
  },
};

export default function () {
  const res = http.get(`${BASE_URL}/public/v1/observations/?parameterCode=co2&limit=100`);
  check(res, { 'status 200': (r) => r.status === 200 });
}
