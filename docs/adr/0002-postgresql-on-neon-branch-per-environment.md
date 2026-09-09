# 0002 — PostgreSQL on Neon, branch per environment

## Context

The API needs a PostgreSQL instance that is reachable from a developer machine,
from CI, and from a deployed container, at no cost for a personal project.

A local-only database means the deployed instance has nowhere to point. A single
shared database means a bad migration applied while experimenting takes the
demo down with it.

## Decision

PostgreSQL 17 on Neon. The Neon project has two branches: `main` for a deployed
instance and `dev` for local work. Branches are copy-on-write, so the second one
costs nothing and isolates a destructive migration.

Two connection strings are configured, and the difference is load-bearing:

- **Pooled** (host contains `-pooler`) — the running application.
- **Direct** (no `-pooler`) — `dotnet ef` migrations.

Neither is committed. Locally they live in user secrets, in a deployed instance
in environment variables using the `ConnectionStrings__Default` form.
`appsettings.json` keeps empty placeholders so the shape of the configuration is
visible without the values.

Integration tests do not use Neon at all. They start PostgreSQL in a container
(ADR 0012), so CI needs no database credential.

## Consequences

The pooler runs in transaction mode and does not support the session-level
operations EF Core performs while applying a migration. Using the pooled string
for `dotnet ef database update` produces intermittent failures that look like
network problems. The design-time factory reads the `Migrations` string
specifically, so the correct endpoint is picked without anyone remembering.

Neon autosuspends idle compute on the free tier, so the first request after a
pause takes up to a couple of seconds and can fail outright.
`EnableRetryOnFailure` is therefore on. That has a consequence of its own: with
a retrying execution strategy, a manual `BeginTransaction` throws at runtime
unless it is wrapped in `Database.CreateExecutionStrategy().ExecuteAsync(...)`.
The transaction behavior in M3 does this in one place.