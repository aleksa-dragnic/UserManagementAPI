# UserManagementAPI

[![CI](https://github.com/aleksa-dragnic/UserManagementAPI/actions/workflows/ci.yml/badge.svg)](https://github.com/aleksa-dragnic/UserManagementAPI/actions/workflows/ci.yml)
[![coverage](https://codecov.io/gh/aleksa-dragnic/UserManagementAPI/branch/main/graph/badge.svg)](https://codecov.io/gh/aleksa-dragnic/UserManagementAPI)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![license](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

A REST API that owns the full lifecycle of users, roles and permissions:
registration, authentication with rotating refresh tokens, permission-based
authorization, and a queryable user directory. It is deliberately narrow in
scope and deep in execution — the domain is small enough to model properly,
which makes it a place to show what a well-built .NET service actually looks
like: a domain model that owns its own invariants, a clean split between the
write and read paths, and an HTTP surface that behaves the way a REST API is
supposed to.

It is a personal project built to a production standard. It is not running a
business and has no users but its author.

## Quick start

```bash
git clone https://github.com/aleksa-dragnic/UserManagementAPI.git
cd UserManagementAPI
docker compose up -d --build
```

That brings up PostgreSQL, applies the migrations, seeds roles, permissions and
an administrator, and starts the API. When `docker compose logs api` shows the
application started, open:

- **http://localhost:8080/scalar** — the API documentation, v1 and v2
- **http://localhost:8080/api** — the root document, links to everything else
- **http://localhost:8080/health/ready** — readiness, including the database

Log in at `POST /api/v1/auth/login` with `admin@umapi.local` /
`Admin-Local-Compose-2026!` (the compose stack only), paste the access token
into Scalar's auth box, and every endpoint is callable from the browser.

`UserManagementAPI.http` in the repository root runs the same flow from an
editor: log in, capture the token, then every endpoint in order. It targets
`dotnet run` on port 5085 by default, with the compose host and password
commented at the top of the file.

## Live demo

**https://usermanagementapi-j1if.onrender.com/scalar**

A read-only account is seeded for it: `demo@umapi.local` / `Demo-Passw0rd-2026!`.
It holds the `Member` role, which grants `users.read` and `roles.read`, so
every GET works and every write answers 403 — which is itself worth seeing.

The instance runs on Render's free tier and spins down after fifteen minutes of
inactivity, so the first request after a quiet period takes up to a minute
while it wakes up. The database is Neon, also free tier.

## Features

**Domain modelling**
- Aggregates that own their invariants: no public setter, every state change
  through a method, every transition tested on both the allowed and the
  refused path
- Value objects (`Email`, `PersonName`, `PasswordHash`) validated in their
  factories and mapped as EF Core owned types
- Enumeration classes instead of C# enums, so a state can carry behaviour
- Domain events dispatched before commit, inside the same transaction; an
  outbox for anything that has to leave the service
- `Result<T>` instead of exceptions for expected failures

**Security**
- Argon2id password hashing with parameters from configuration
- Short-lived JWTs with rotating refresh tokens, stored only as hashes, with
  reuse detection that revokes the whole chain
- Permission-based authorization — `[HasPermission("users.write")]`, never a
  role name in code, no bare `[Authorize]` anywhere
- Append-only audit log, enforced by a database trigger, recording which fields
  changed and never their values
- Per-endpoint rate limiting, security headers, explicit CORS, request size cap

**HTTP surface**
- RFC 9457 problem details on every failure, with a stable `errorCode`
- Paging, filtering, searching and whitelisted sorting, with metadata in
  `X-Pagination`
- Conditional GET: strong ETags and 304 on `If-None-Match`
- URL-segment versioning with v1 and v2 documented separately
- Opt-in hypermedia behind `application/vnd.umapi.hateoas+json`, a root
  document, `OPTIONS` and `HEAD`

**Operations**
- OpenTelemetry traces and metrics, a correlation id in every log line and
  response, liveness and readiness split
- Structured logging with Serilog, rendered as JSON in production so a log record cannot be forged from a request path
- Multi-stage Docker build, compose stack with PostgreSQL and an OTLP collector
- GitHub Actions: build with warnings as errors, tests against a real
  PostgreSQL in a container, coverage

## Tech stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10, C# 14 |
| Web | ASP.NET Core, controller-based |
| Database | PostgreSQL 17 (Neon in the cloud, Testcontainers in tests) |
| Data access | EF Core 10 + Npgsql, raw configurations, no in-memory provider |
| Validation | FluentValidation for command shape, the aggregate for invariants |
| Mapping | Mapperly, source-generated |
| Auth | JWT bearer, Argon2id, rotating refresh tokens, permission policies |
| Docs | OpenAPI 3.1 + Scalar |
| Telemetry | Serilog, OpenTelemetry |
| Testing | xUnit, FluentAssertions, NSubstitute, Testcontainers |
| CI | GitHub Actions, Docker |

**Deliberately not used.** No mediator library — the dispatcher is eighty lines
and owes nobody a licence. No AutoMapper — Mapperly does the same at compile
time. No repository over queries — `IQueryable` behind an interface buys
nothing. No in-memory database provider — it passes tests that fail against
PostgreSQL.

## Testing

| Layer | Type | What it covers |
|---|---|---|
| Domain | Unit | Every aggregate method, every invariant, every value object — no mocks, the domain has no dependencies |
| Application | Unit | Handler orchestration with faked repositories, behaviors, validators, query composition |
| Infrastructure | Integration | Mappings, migrations, generated SQL, event dispatch, outbox, concurrency — against real PostgreSQL |
| API | Functional | Real HTTP over the full stack: auth flows, permission gates, paging, conditional GET, versioning, hypermedia, hardening |

```bash
dotnet test UserManagementAPI.sln --configuration Release
```

The integration tests start PostgreSQL in a container, so Docker has to be
running; no connection string is needed.

## Documentation

- [`docs/architecture.md`](docs/architecture.md) — layers, both request paths,
  model types, aggregate boundaries, schema, middleware order
- [`docs/adr/README.md`](docs/adr/README.md) — seventeen architecture decision
  records, each written in the pull request that implemented it
- [`docs/BUILD-PLAN.md`](docs/BUILD-PLAN.md) — the plan the project was built
  to, milestone by milestone

## Configuration

Nothing secret is in the repository. Locally, secrets live in user secrets; in
a deployed instance they are environment variables.

| Key | Meaning |
|---|---|
| `ConnectionStrings:Default` | The pooled Neon connection string the application uses |
| `ConnectionStrings:Migrations` | The direct one `dotnet ef` uses |
| `Jwt:SigningKey` | At least 64 characters; startup fails fast without it |
| `Seed:AdministratorPassword` | The bootstrap administrator's password |
| `Database:MigrateOnStartup` | Off in production — migrations are a deliberate step |
| `Database:SeedOnStartup` | Off in production — switched on once for the demo instance |
| `OpenApi:Enabled` | Publishes the OpenAPI document and Scalar outside development |
| `Cors:AllowedOrigins` | Empty by default; no cross-origin request is allowed until an origin is named |
| `RateLimiting:*` | Permit limits and windows for the auth, read and write policies |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Set it and traces and metrics are exported; leave it unset and nothing is |

## License

MIT — see [LICENSE](LICENSE).
