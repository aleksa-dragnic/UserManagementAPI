# AGENTS.md

Instructions for a coding agent working in this repository. A person can read
them too: they are the same rules the maintainer works to. How the agent and the
maintainer divide the work is in [`docs/AI-WORKFLOW.md`](docs/AI-WORKFLOW.md),
and the decision behind it is
[ADR 0022](docs/adr/0022-working-with-a-coding-agent.md).

When these instructions and the code disagree, the code is the current state and
this file is out of date. Say so in the pull request rather than guessing which
one is right.

## What this is

A REST API for users, roles and permissions on .NET 10 and PostgreSQL 17.
Clean architecture in four projects, CQRS with a hand-written dispatcher, and a
domain model that owns its invariants. It is a personal project built to a
production standard; the live instance is a free-tier demo.

Read before changing anything:

1. [`docs/architecture.md`](docs/architecture.md): layers, the two request
   paths, middleware order, schema
2. [`docs/adr/README.md`](docs/adr/README.md): the decisions; do not reopen one
   without saying so
3. [`docs/TESTING.md`](docs/TESTING.md): what each test level is for, and the
   rules tests follow
4. [`docs/DATABASE.md`](docs/DATABASE.md): before touching a migration

## Layout

| Project | Holds | May reference |
|---|---|---|
| `src/UserManagementAPI.Domain` | Aggregates, value objects, enumerations, domain events, repository interfaces, `Result` | nothing but the BCL; no `PackageReference` |
| `src/UserManagementAPI.Application` | Commands, queries, handlers, validators, pipeline behaviors, abstractions | `Domain`; no EF Core or ASP.NET Core |
| `src/UserManagementAPI.Infrastructure` | `AppDbContext`, configurations, migrations, repositories, query handlers, Argon2, JWT, outbox | `Domain`, `Application` |
| `src/UserManagementAPI.Api` | Controllers, HTTP contracts, mappers, filters, errors, composition root | `Application`; `Infrastructure` only in `Program.cs` and `Extensions/` |

`LayeringTests` and `ApiLayeringTests` fail the build when a reference crosses
these lines (ADR 0001).

## Commands

Run from the repository root. The SDK comes from `global.json`.

```bash
dotnet restore UserManagementAPI.sln
dotnet build UserManagementAPI.sln --configuration Release      # warnings are errors

# Every test; Docker must be running (Testcontainers starts PostgreSQL).
dotnet test UserManagementAPI.sln --configuration Release

# Without Docker: the two unit test projects.
dotnet test tests/UserManagementAPI.Domain.UnitTests --configuration Release
dotnet test tests/UserManagementAPI.Application.UnitTests --configuration Release

# The whole stack: PostgreSQL, migrations, seed, API on http://localhost:8080.
docker compose up -d --build

# The API alone, on http://localhost:5085, against the database in user secrets.
dotnet run --project src/UserManagementAPI.Api

# A new migration, then the idempotent script CI also produces.
dotnet tool restore
dotnet dotnet-ef migrations add <Name> \
  --project src/UserManagementAPI.Infrastructure \
  --startup-project src/UserManagementAPI.Api
dotnet dotnet-ef migrations script --idempotent \
  --project src/UserManagementAPI.Infrastructure \
  --startup-project src/UserManagementAPI.Api
```

`dotnet-ef` builds its context from `ConnectionStrings:Migrations`, the direct
connection string, from user secrets or the environment. `script` connects to
nothing, so any value works there, as in CI:
`ConnectionStrings__Migrations="Host=localhost;Database=unused"`. Never point
`database update` at the deployed database; that step is the maintainer's
(`docs/DATABASE.md`).

CI (`.github/workflows/ci.yml`) runs the same restore, build and test, scripts
the migrations and builds the container image. CodeQL runs beside it.

## Conventions

- **Business rules live in the aggregate.** A handler orchestrates; it decides
  only what the aggregate cannot see (the caller, uniqueness in the database).
  No public setters on domain types; every state change is a method.
- **Expected failures are `Result`, not exceptions.** Every error has a stable
  `errorCode` and reaches the client as RFC 9457 problem details, its status
  following the error's type.
- **Writes and reads take different paths.** A write goes through a command, the
  dispatcher's behaviors and a repository. A read is a query handler in
  Infrastructure that projects with `AsNoTracking` straight into a DTO (ADR
  0016).
- **Authorization is by permission**: `[HasPermission("users.write")]`. Never a
  role name in code, never a bare `[Authorize]` (ADR 0010).
- **HTTP contracts are separate from commands** (ADR 0011) and mapped with
  Mapperly (ADR 0014).
- **Tests** follow `docs/TESTING.md`: real PostgreSQL, never the in-memory
  provider; names that read as sentences; a bug fix starts with a failing test.
- **Packages** are versioned centrally in `Directory.Packages.props`. A new
  dependency needs a reason in the pull request, and its licence checked:
  FluentAssertions stays on 7.x for that reason.
- **Style**: `.editorconfig`, file-scoped namespaces, UTF-8.

## Git and pull requests

- One branch per unit of work, named as in `docs/BUILD-PLAN.md` section 4.2
  (`feat/<area>-<description>`, `fix/...`, `docs/...`, `ci/...`).
- Conventional Commits: `type(scope): imperative subject`, and a body that says
  why. Scopes: `domain`, `application`, `infrastructure`, `api`, `tests`, `ci`,
  `docs`.
- Every commit an agent wrote or co-wrote ends with
  `Co-Authored-By: Claude <noreply@anthropic.com>`.
- The pull request follows `.github/pull_request_template.md`, including the
  AI assistance section. Pull requests are squash-merged.

## Definition of done

Before saying a change is done, the agent has run, and reports with their
output:

- [ ] `dotnet build` in Release with no warnings
- [ ] the test projects the change touches, and the full suite when Docker is
      available; a suite that could not run is named, not skipped silently
- [ ] for a new endpoint: it appears correctly in the OpenAPI document, with its
      problem details
- [ ] for a schema change: a migration, and `MigrationsTests` passing
- [ ] no secret, connection string or key in the diff
- [ ] the documentation the change makes wrong is corrected in the same pull
      request (README, `docs/`, Known limits)

A failing test or build is reported as failing, with its output. Never claim a
result that was not run.

## What the agent does not do

- **No history rewriting.** No force push, no rebase or amend of a pushed
  commit, never on `main`.
- **No merge, no closing a pull request or issue, no deleting a branch** without
  the maintainer's explicit go-ahead for that specific item.
- **No decisions recorded as made.** Architecture decisions and ADRs are the
  maintainer's. The agent may propose one and draft its text; the maintainer
  decides and sets its status.
- **No change to security design on its own initiative**: authentication, token
  handling, password hashing, authorization policies, CORS, security headers,
  rate limits. Work there follows a plan the maintainer wrote, and the
  maintainer reads every line and the assertions of its tests.
- **No secrets.** It never reads, writes or prints a credential, and never puts
  one in configuration, a test or a log.
- **No repository or cloud settings**: branch protection, secrets, Render,
  Neon, Codecov. Those stay with the maintainer.
- **No weakening a check to get green**: no skipped test, no lowered threshold,
  no suppressed warning, no `--no-verify`, without saying so and why in the
  pull request.
