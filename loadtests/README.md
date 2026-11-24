# Load test

`users.js` puts the user directory's read and write paths under load with
[k6](https://grafana.com/docs/k6/) and fails when a threshold is missed (#116).
It runs against the **compose stack**, never against the free-tier demo:
that instance has one small container, sleeps when idle, and its rate limits
would answer most of the test with 429.

## What it does

| Scenario | Model | Rate | One iteration |
|---|---|---|---|
| `reads` | open, `constant-arrival-rate` | 50 iterations/s (`READ_RATE`) | a search, a user's detail, the detail again with `If-None-Match` (304) |
| `writes` | open, `constant-arrival-rate` | 5 iterations/s | a registration with an `Idempotency-Key`, the detail, an update with `If-Match` |

Both run for two minutes, together. They are open-model: a slow response does
not lower the load, it shows up as latency. A closed model without pauses
saturates the machine on its own and turns every latency threshold into a
measure of the machine, so capacity is measured separately (below).

**Thresholds**, all of which fail the run:

| | p95 | p99 |
|---|---|---|
| reads | < 300 ms | < 1 s |
| writes (a registration hashes with Argon2id on purpose) | < 800 ms | < 1.5 s |
| failed requests | < 1 % | |
| checks (expected status) | > 99 % | |

Environment variables: `BASE_URL`, `ADMIN_EMAIL`, `ADMIN_PASSWORD` (the compose
stack's by default), `READ_RATE`, `SCENARIO=reads|writes` (one alone),
`CAPACITY=1` (twenty readers with no pauses and no latency thresholds: the
throughput it reaches is the read capacity), `RESULTS_DIR` (where
`summary.json` goes).

## Running it

```bash
export COMPOSE_PROJECT_NAME=umapi-loadtest
F="-f docker-compose.yml -f loadtests/compose.loadtest.yml"

docker compose $F up -d --build --wait db api
# The API has no container healthcheck; give it a few seconds, then seed.
docker compose $F exec -T api dotnet UserManagementAPI.Api.dll seed-demo-directory

docker run --rm --network umapi-loadtest_default \
  -e BASE_URL=http://api:8080 -e RESULTS_DIR=/scripts/results \
  -v "$PWD/loadtests:/scripts" \
  grafana/k6:2.4.0@sha256:d41bb79cd8de5a7ad619b53570e3712633e8bff0937010a5c6c57a4747e7a276 \
  run /scripts/users.js

docker compose $F down -v   # the load test's own database goes with it
```

`compose.loadtest.yml` lifts the per-user read and write limits, turns off
telemetry export, and gives the stack **a database volume of its own**, so the
hundreds of users a run registers never reach the development database.

k6 runs inside the compose network and talks to `api:8080`, so nothing depends
on what else listens on the host's port 8080.

On GitHub: Actions → **Load test** → Run workflow. It does the same on the
runner and keeps `summary.json` as the `k6-summary` artifact.

Results and their machine are recorded in [`docs/TESTING.md`](../docs/TESTING.md).
The GitHub runner meets every threshold. A Docker Desktop VM with about
3.5 GiB of memory does not, by seconds at p99, because the machine runs out
before the service does (#128): read a local run against the runner's numbers
before calling it a regression.
