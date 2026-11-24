// Load test of the user directory's read and write paths (#116).
// Run against the compose stack, never the free-tier demo: loadtests/README.md.
import http from 'k6/http';
import { check, fail } from 'k6';
import exec from 'k6/execution';

const BASE = __ENV.BASE_URL || 'http://localhost:8080';
const ADMIN_EMAIL = __ENV.ADMIN_EMAIL || 'admin@umapi.local';
const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD || 'Admin-Local-Compose-2026!';
const SEARCHES = ['ana', 'petrovic', 'jovan', 'marko', 'ić', 'milica'];
// One scenario alone with SCENARIO=reads or SCENARIO=writes, to tell which one
// a regression is in. Both run by default.
const ONLY = __ENV.SCENARIO;
const RESULTS = __ENV.RESULTS_DIR || 'loadtests/results';

// Both scenarios are open-model: a fixed arrival rate, so a slow response does
// not lower the load, it shows up as latency. A closed model with no pauses
// (constant VUs) saturates the machine by itself and makes every latency
// threshold meaningless; capacity is measured separately (CAPACITY=1).
const scenarios = {
    // Administrators browsing: 50 iterations a second, three requests each
    // (a search, a detail, the detail read again with its ETag).
    reads: {
      executor: 'constant-arrival-rate',
      exec: 'reads',
      rate: Number(__ENV.READ_RATE || 50),
      timeUnit: '1s',
      duration: '2m',
      preAllocatedVUs: 20,
      maxVUs: 100,
    },
    // Registrations and edits: 5 a second, three requests each.
    writes: {
      executor: 'constant-arrival-rate',
      exec: 'writes',
      rate: 5,
      timeUnit: '1s',
      duration: '2m',
      preAllocatedVUs: 10,
      maxVUs: 30,
    },
};

// CAPACITY=1: twenty readers with no pauses, no thresholds on latency. The
// throughput it reaches is the read capacity of the machine it runs on.
if (__ENV.CAPACITY) {
  scenarios.reads = { executor: 'constant-vus', exec: 'reads', vus: 20, duration: '1m' };
}

export const options = {
  scenarios: ONLY ? { [ONLY]: scenarios[ONLY] } : scenarios,
  // Tail latency and errors, not averages. A run that misses one fails.
  thresholds: {
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
    'http_req_duration{scenario:reads}': __ENV.CAPACITY ? [] : ['p(95)<300', 'p(99)<1000'],
    // A registration hashes a password with Argon2id (19 MiB, two passes) on
    // purpose; the write budget allows for it.
    'http_req_duration{scenario:writes}': ['p(95)<800', 'p(99)<1500'],
  },
  summaryTrendStats: ['avg', 'p(50)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const login = http.post(
    `${BASE}/api/v1/auth/login`,
    JSON.stringify({ email: ADMIN_EMAIL, password: ADMIN_PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } },
  );
  if (login.status !== 200) fail(`login answered ${login.status}`);

  const token = login.json('accessToken');
  const list = http.get(`${BASE}/api/v1/users?pageSize=50`, auth(token));
  if (list.status !== 200) fail(`the directory answered ${list.status}`);

  const users = list.json();
  const ids = (Array.isArray(users) ? users : users.items).map((user) => user.id);
  if (ids.length < 10) fail(`seed the directory first: only ${ids.length} users`);

  return { token, ids };
}

export function reads(data) {
  const params = auth(data.token);
  const term = SEARCHES[exec.scenario.iterationInTest % SEARCHES.length];

  const page = http.get(`${BASE}/api/v1/users?searchTerm=${encodeURIComponent(term)}&pageSize=10`, {
    ...params,
    tags: { name: 'GET /users?searchTerm' },
  });
  check(page, { 'list 200': (response) => response.status === 200 });

  const id = data.ids[exec.scenario.iterationInTest % data.ids.length];
  const detail = http.get(`${BASE}/api/v1/users/${id}`, { ...params, tags: { name: 'GET /users/{id}' } });
  check(detail, { 'detail 200': (response) => response.status === 200 });

  // The console reads again with the validator it kept (ADR 0013 in the console).
  const etag = detail.headers.Etag || detail.headers.ETag;
  if (etag) {
    const again = http.get(`${BASE}/api/v1/users/${id}`, {
      headers: { ...params.headers, 'If-None-Match': etag },
      tags: { name: 'GET /users/{id} If-None-Match' },
    });
    check(again, { 'conditional 304': (response) => response.status === 304 });
  }
}

export function writes(data) {
  const params = auth(data.token);
  const n = `${exec.vu.idInTest}-${exec.scenario.iterationInTest}-${Date.now()}`;
  const email = `load-${n}@example.com`;

  const created = http.post(
    `${BASE}/api/v1/users`,
    JSON.stringify({ email, firstName: 'Load', lastName: 'Test', password: 'Load-Test-Passw0rd!' }),
    {
      headers: { ...params.headers, 'Content-Type': 'application/json', 'Idempotency-Key': `load-${n}` },
      tags: { name: 'POST /users' },
    },
  );
  if (!check(created, { 'register 201': (response) => response.status === 201 })) return;

  const id = created.json('id');
  const detail = http.get(`${BASE}/api/v1/users/${id}`, { ...params, tags: { name: 'GET /users/{id}' } });
  const version = detail.json('version');

  const updated = http.put(
    `${BASE}/api/v1/users/${id}`,
    JSON.stringify({ email, firstName: 'Load', lastName: 'Edited' }),
    {
      headers: { ...params.headers, 'Content-Type': 'application/json', 'If-Match': `"${version}"` },
      tags: { name: 'PUT /users/{id} If-Match' },
    },
  );
  check(updated, { 'update 204': (response) => response.status === 204 });
}

export function handleSummary(data) {
  return { [`${RESULTS}/summary.json`]: JSON.stringify(data, null, 2), stdout: textSummary(data) };
}

function auth(token) {
  return { headers: { Authorization: `Bearer ${token}` } };
}

// The few numbers worth reading, one line each.
function textSummary(data) {
  const lines = [];
  for (const scenario of ['reads', 'writes']) {
    const metric = data.metrics[`http_req_duration{scenario:${scenario}}`];
    if (metric) {
      const v = metric.values;
      lines.push(
        `${scenario.padEnd(7)} p50 ${v['p(50)'].toFixed(1)} ms  p95 ${v['p(95)'].toFixed(1)} ms  p99 ${v['p(99)'].toFixed(1)} ms  max ${v.max.toFixed(1)} ms`,
      );
    }
  }
  lines.push(`requests ${data.metrics.http_reqs.values.count} (${data.metrics.http_reqs.values.rate.toFixed(1)}/s)`);
  lines.push(`failed   ${(data.metrics.http_req_failed.values.rate * 100).toFixed(2)} %`);
  lines.push(`checks   ${(data.metrics.checks.values.rate * 100).toFixed(2)} %`);
  const failed = Object.entries(data.metrics).filter(([, m]) => m.thresholds && Object.values(m.thresholds).some((t) => !t.ok));
  lines.push(failed.length ? `THRESHOLDS FAILED: ${failed.map(([name]) => name).join(', ')}` : 'all thresholds met');
  return lines.join('\n') + '\n';
}
