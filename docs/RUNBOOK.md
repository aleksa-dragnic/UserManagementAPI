# Runbook

What to do when the deployed instance misbehaves. The instance is one Render web
service (`render.yaml`, free plan, Frankfurt) on a Neon project with two
branches, `main` for the deployed instance and `dev` for local work
(`docs/DATABASE.md`). It is a demo with no users but its author; the steps are
written as if it were not, because that is the habit worth having.

Every step that touches Render, Neon or a secret is done by the maintainer from
their own accounts. Nothing here is automated, and a coding agent does not run
any of it (`AGENTS.md`).

## First look

1. **Is it up?** `curl -i https://api.aleksadragnic.com/health/live` answers 200
   when the process runs. `…/health/ready` answers 200 when it can also reach
   the database; Render's health check uses `ready` (`render.yaml`).
2. **Is it asleep?** The free plan sleeps after fifteen minutes. The first
   request takes up to a minute; that is a wake-up, not an outage (README,
   Live demo).
3. **What does it say?** Render dashboard → the service → Logs. Production logs
   are JSON (ADR 0018); every line of a request carries its `CorrelationId`,
   and the same id is in the response's `X-Correlation-Id`, so one failing
   request can be found from the client's side.
4. **Traces**, if `OTEL_EXPORTER_OTLP_ENDPOINT` is set: `docs/OBSERVABILITY.md`.

## The instance does not start

Symptoms: the deploy fails, or Render restarts the service in a loop.

- **`Jwt:SigningKey must be at least 64 characters`**: the key is missing or
  short. Set `Jwt__SigningKey` in the Render dashboard (64+ characters) and
  redeploy. Startup fails fast on purpose (`JwtTokenService`).
- **A configuration error at startup**: compare the dashboard's environment with
  `render.yaml`. Every secret is listed there by name with `sync: false`.
- **The image does not build**: the same Dockerfile builds in CI on every pull
  request, so a red `main` names the cause first. A base image digest moved by
  Dependabot is the likeliest new cause (#83).
- **Roll back**: Render → Deploys → redeploy the last good one. Rolling the code
  back does not roll the schema back; see the next section first if the failed
  deploy carried a migration.

## Readiness fails on the database

Symptoms: `/health/live` 200, `/health/ready` 503, requests answer 500.

1. **Neon compute suspended or waking**: Neon suspends idle compute.
   `EnableRetryOnFailure` absorbs a wake-up (ADR 0002); a readiness failure
   that clears within a minute needs nothing.
2. **Neon status**: the Neon console, and Neon's status page. An incident on
   their side is waited out; there is no second database to fail over to, and
   that is a stated limit of the demo.
3. **Connection string**: `ConnectionStrings__Default` must be the **pooled**
   endpoint of the `main` branch. A direct endpoint works until it runs out of
   connections; the `dev` branch's string serves the wrong data.
4. **A migration the code needs is missing**: the code was deployed before its
   migration. Apply it (next section), and readiness recovers without a
   redeploy.

## A migration has to be applied, or rolled forward

Migrations are a deliberate step, never run on startup in production
(`docs/DATABASE.md`).

1. Read the `migrations` artifact of the CI run for the commit being deployed:
   an idempotent script, safe to run twice.
2. Apply it **before** deploying the code that needs it, against the **direct**
   connection string of `main` (`ConnectionStrings:Migrations`), either with
   `psql -f migrations.sql` or with `dotnet dotnet-ef database update` from a
   workstation.
3. A migration that went wrong is **rolled forward**, not back: a new migration
   that repairs it, through a pull request like any other. Restoring the
   database (below) is for data loss, not for a bad schema change.

## The signing key has to be rotated

When the key may have leaked, or as routine.

1. Generate 64+ random characters and set `Jwt__SigningKey` in the Render
   dashboard. Render redeploys.
2. **What it does**: every access token signed with the old key is refused from
   that moment, so every client gets one 401. Access tokens live fifteen
   minutes at most, so nothing waits longer than that anyway.
3. **What it does not do**: refresh tokens are not signed. They are random
   values stored as SHA-256 hashes (ADR 0009), unaffected by the key, so a
   client's next refresh issues a new access token and the session continues.
   To end every session as well, revoke the refresh tokens: there is no
   endpoint for that, so it is SQL against `main`, through a pull request that
   records why.

## The outbox stops draining

Symptoms: `outbox_messages` rows with `processed_on_utc IS NULL` pile up.

1. **Logs**: `Outbox poll failed` means the poll itself fails (usually the
   database); `Outbox message … abandoned after N attempts` means one message
   failed every attempt and was set aside so it does not block the rest.
2. **What is stuck**:
   `SELECT id, type, attempts, next_attempt_on_utc, error FROM outbox_messages WHERE processed_on_utc IS NULL ORDER BY occurred_on_utc;`
3. **An abandoned message** (`attempts` at `Outbox:MaxAttempts`) is retried by
   setting `attempts = 0, next_attempt_on_utc = NULL` once the cause is fixed.
   Consumers are idempotent by contract, so a retry is safe (ADR 0008).
4. **Several replicas** are safe: each claims its batch with
   `FOR UPDATE SKIP LOCKED` (#85).

## A user cannot sign in: 429 Auth.TooManyFailedAttempts

The login throttle (ADR 0024): five wrong passwords in a row for that email,
and sign-in for it is refused for a delay that doubles up to fifteen minutes.
It ends by itself; `Retry-After` says when.

- **Nothing to unlock.** There is no lockout. If the user cannot wait, the
  row can be deleted. It is keyed by the hash, which is computed the same way
  the API computes it:
  `DELETE FROM login_failures WHERE email_hash = upper(encode(sha256(lower(trim('<email>'))::bytea), 'hex'));`
- **Many rows at once** mean guessing from many addresses. The throttle is
  doing its job; the logs show `Auth.TooManyFailedAttempts` refusals in the
  login metrics' `error_code` tag.

## Backups and restore

Neon keeps the history of each branch for the restore window of the project's
plan, and can restore a branch, or create a new one, at any instant inside it.
There is no other backup.

| | Target | Where it comes from |
|---|---|---|
| **RPO**, data that may be lost | none inside the restore window | point-in-time restore from Neon's history, not snapshots |
| **RTO**, time to serve again | to be set by the drill below | create a branch at the instant, repoint `ConnectionStrings__Default`, redeploy |
| **Beyond the window** | nothing can be restored | the stated limit of a free demo |

**Restore, when data was lost:**

1. Neon console → the project → Branches → create a branch from `main` **at the
   instant before the loss** (point in time). Do not restore `main` in place
   until the new branch has been checked.
2. Check the new branch: connect with `psql` and look at what was lost.
3. Point the service at it: set `ConnectionStrings__Default` to the new
   branch's **pooled** string in the Render dashboard; Render redeploys.
4. Check `/health/ready`, sign in as the demo account, read a user.
5. Write down what happened in the drill log below, with the times.

## Restore drill log

A restore that has never been done is an assumption. The drill restores into a
scratch branch, never into `main`, and is timed from step 1 to step 4 above
(with step 3 pointed at a scratch instance or skipped and timed separately).

| Date | Restored to | Time to a readable branch | Time to serving | Notes |
|---|---|---|---|---|
| _not yet performed_ | | | | The first drill sets the RTO above (#117). |
