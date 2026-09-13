# UserManagementAPI — Build Plan

A production-shaped user and access management API built on ASP.NET Core and PostgreSQL.

This document is the working plan for the project: how it is structured, how it gets built, and the rules I follow while building it. It lives in the repository at `docs/BUILD-PLAN.md`.

---

## 1. What this project is

A REST API that owns the full lifecycle of users, roles and permissions for a multi-service product: registration, authentication with rotating refresh tokens, permission-based authorization, and a queryable user directory.

It is deliberately narrow in scope and deep in execution. The domain is small enough to model properly, which makes it a good place to show what a well-built .NET service actually looks like: a rich domain model that owns its own invariants, a clean separation between write and read paths, and an HTTP surface that behaves the way a REST API is supposed to behave.

**Design goals**

| Goal | How it shows up |
|---|---|
| The domain owns its rules | No business logic in controllers, handlers or services. Invariants live in the aggregate. |
| Write and read are different problems | Commands go through the domain model. Queries project straight to DTOs. |
| Infrastructure is replaceable | The domain project has zero infrastructure dependencies. |
| The HTTP surface is honest | Correct status codes, RFC 9457 problem details, ETags, pagination metadata, versioning. |
| Everything is verifiable | Unit tests on the domain, integration tests against a real PostgreSQL, functional tests over the full stack. |

**Non-goals.** This is not a general-purpose identity provider. It does not implement OAuth 2.1 or OpenID Connect flows, and it does not attempt multi-tenancy. Both are deliberate scope decisions, recorded as ADRs.

---

## 2. Stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10 (LTS), C# 14 |
| Web | ASP.NET Core, controller-based API |
| Database | PostgreSQL 17 on Neon |
| Data access | EF Core 10 + Npgsql |
| Validation | FluentValidation (commands), data annotations (request contracts) |
| Mapping | Mapperly (source-generated) |
| Auth | JWT bearer, rotating refresh tokens, permission-based policies |
| Logging | Serilog, structured, correlation ID per request |
| Docs | OpenAPI 3.1 + Scalar UI |
| Testing | xUnit, FluentAssertions, NSubstitute, Testcontainers |
| CI | GitHub Actions |
| Container | Docker, multi-stage build |

**Deliberate omissions.** No mediator library — the dispatcher is ~80 lines and removes a dependency with licensing constraints. No AutoMapper — Mapperly generates the same mapping at compile time with no reflection cost. No repository abstraction over queries — queries use `DbContext` directly, because wrapping `IQueryable` in a repository buys nothing and costs flexibility.

---

## 3. Prerequisites

- .NET 10 SDK — `dotnet --version` reports 10.x
- Docker, for Testcontainers and for `docker compose up`
- `dotnet tool install --global dotnet-ef`

That is the whole list. Nothing in the checkout needs configuring before
`docker compose up -d --build` brings the stack up, and no secret is required to
build or to run the tests — the integration suite starts its own PostgreSQL in a
container. Section 5 covers the connection strings a workstation or a deployed
instance needs.

---
## 4. Git

### 4.1 Repository settings to apply immediately

- **Description:** "User and access management API — ASP.NET Core, PostgreSQL, clean architecture, CQRS."
- **Topics:** `dotnet`, `aspnetcore`, `csharp`, `postgresql`, `clean-architecture`, `cqrs`, `ddd`, `rest-api`, `jwt`
- **License:** MIT.
- **Branch protection on `main`:** require a pull request, require the CI status check to pass, disallow force pushes.

Branch protection matters more than it looks. It makes every change go through a PR, which produces a readable history someone can scroll through — and it prevents a tired 2 a.m. `git push --force` from erasing a week.

### 4.2 Branch naming

```
feat/<area>-<short-description>      feat/domain-user-aggregate
fix/<area>-<short-description>       fix/auth-refresh-expiry
refactor/<area>-<short-description>  refactor/api-versioning-setup
docs/<short-description>             docs/adr-cqrs-dispatcher
chore/<short-description>            chore/bump-npgsql
```

One branch per PR. One PR per unit of work from the plan in section 7. A branch lives for a day or two, never a week.

### 4.3 Commit convention

Conventional Commits, enforced by habit rather than tooling:

```
<type>(<scope>): <subject in imperative mood, lowercase, no period>

<body: what changed and why, wrapped at 72 columns>
```

**Types:** `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `build`, `ci`, `chore`.

**Scopes:** `domain`, `application`, `infrastructure`, `api`, `tests`, `ci`, `docs`.

Good:

```
feat(domain): enforce unique email on user registration

Registration went through UserManager without checking for an existing
address, so a duplicate produced a database constraint violation surfaced
as a 500. The check now lives in the aggregate factory and returns a
domain error, which the handler translates to 409.
```

Bad: `update stuff`, `fix bug`, `WIP`, `asdf`.

The body answers *why*. The diff already shows *what*. Anyone reading the history later — including me in three months — needs the reason, not a restatement of the code.

### 4.4 Commit rhythm

- Commit when a thought is complete and the build is green. Roughly every 30–90 minutes of work.
- Never commit code that does not compile.
- Never commit commented-out code or `TODO` without a linked issue.
- Push at the end of every session. Unpushed work only exists on one machine.
- Rebase feature branches on `main` before opening a PR; do not merge `main` into the branch.
- Squash-merge PRs into `main`, so `main` reads as one commit per unit of work.
- Never force-push `main`. Force-push on a personal feature branch is fine.

### 4.5 Pull request rules

Every PR gets a description that answers three questions:

```markdown
## What
One or two sentences.

## Why
The reasoning. Link the ADR if the PR implements a decision.

## Notes
Trade-offs, anything deliberately left out, anything the next PR picks up.
```

Self-review the diff on GitHub before merging. Reading your own change as a diff catches things the editor hides — stray debug logging, a file you did not mean to touch, an inconsistent naming choice.

Tag a release at the end of every milestone: `v0.1.0`, `v0.2.0`, and `v1.0.0` when M6 lands.

---

## 5. Neon PostgreSQL

### 5.1 Connection strings and where they live

Neon exposes two endpoints for the same database, and the difference matters:

| Endpoint | Host contains | Use for |
|---|---|---|
| Pooled | `-pooler` | The running application |
| Direct | no `-pooler` | `dotnet ef` migrations |

The pooler runs in transaction mode and does not support the session-level
operations EF Core uses while applying a migration, so the pooled string turns
`dotnet ef database update` into an intermittent failure that reads like
anything but its cause. Both endpoints need `sslmode=require`.

Two Neon branches, copy-on-write and therefore free: `dev` for a workstation,
`main` for a deployed instance. A bad migration applied locally cannot reach the
other one (ADR 0002).

No connection string is in `appsettings.json` and none is in a commit.
`appsettings.json` carries the non-secret configuration and empty placeholders,
so the shape of it is visible in the repository without the values. Locally the
values live in user secrets; a deployed instance takes them as environment
variables in the `ConnectionStrings__Default` double-underscore form. CI needs no
database credential at all.
### 5.2 Resilience

Neon autosuspends idle compute. The first request after a suspend takes a few hundred milliseconds to a couple of seconds while it resumes, and can fail outright. Enable retry:

```csharp
options.UseNpgsql(connectionString, npgsql =>
{
    npgsql.EnableRetryOnFailure(maxRetryCount: 5,
                                maxRetryDelay: TimeSpan.FromSeconds(10),
                                errorCodesToAdd: null);
    npgsql.MigrationsAssembly("UserManagementAPI.Infrastructure");
});
```

One consequence to remember: with a retrying execution strategy, any manual `BeginTransaction` must be wrapped in `Database.CreateExecutionStrategy().ExecuteAsync(...)`, or EF throws at runtime. The transaction behavior in the pipeline handles this in one place.

---

## 6. Architecture

### 6.1 Solution layout

```
UserManagementAPI/
├── .github/workflows/ci.yml
├── docs/
│   ├── BUILD-PLAN.md
│   ├── architecture.md
│   └── adr/
│       ├── 0001-clean-architecture-layering.md
│       └── ...
├── src/
│   ├── UserManagementAPI.Domain/
│   ├── UserManagementAPI.Application/
│   ├── UserManagementAPI.Infrastructure/
│   └── UserManagementAPI.Api/
├── tests/
│   ├── UserManagementAPI.Domain.UnitTests/
│   ├── UserManagementAPI.Application.UnitTests/
│   └── UserManagementAPI.IntegrationTests/
├── Directory.Build.props
├── docker-compose.yml
├── Dockerfile
├── UserManagementAPI.sln
├── LICENSE
└── README.md
```

### 6.2 Project dependencies

```
Api ──────────► Application ──────────► Domain
 │                                        ▲
 └╌╌╌╌╌╌► Infrastructure ─────────────────┘
        (composition root only)
```

- `Domain` references nothing but the BCL. No EF Core package, no ASP.NET package.
- `Application` references `Domain`. It defines interfaces (`IUserRepository`, `IUnitOfWork`, `IPasswordHasher`, `ITokenService`) that `Infrastructure` implements.
- `Infrastructure` references `Domain` and `Application`.
- `Api` references `Application`, and `Infrastructure` **only in `Program.cs`** to wire the container. No controller may name an infrastructure type.

The rule is enforceable, not just documented. `LayeringTests` in `Application.UnitTests` asserts that `Domain` references nothing outside the base class library and that `Application` references neither EF Core nor ASP.NET; `ApiLayeringTests` in `UserManagementAPI.IntegrationTests` asserts that no controller names an infrastructure type, and sits there because that is the only project referencing both `Api` and `Infrastructure`. All three run in CI, so reaching through a layer fails the build.

### 6.3 Project internals

**Domain**

```
Common/                 Entity, AggregateRoot, ValueObject, Enumeration,
                        IDomainEvent, Error, Result<T>, DomainException
Users/                  User, UserRole, UserStatus,
                        Email, PersonName, PasswordHash,
                        UserRegisteredEvent, UserLockedEvent,
                        IUserRepository
Roles/                  Role, Permission, RolePermission, IRoleRepository
```

**Application**

```
Abstractions/           ICommand, IQuery, ICommandHandler, IQueryHandler,
                        IDispatcher, IUnitOfWork, ICurrentUser
Behaviors/              LoggingBehavior, ValidationBehavior, TransactionBehavior
Users/
  Commands/             RegisterUser/, UpdateUser/, AssignRole/, LockUser/
                        (each folder: Command, Handler, Validator)
  Queries/              GetUsers/, GetUserById/
  Dtos/                 UserDto, UserDetailsDto
  EventHandlers/        WriteAuditEntryOnUserRegistered
Common/                 PagedList, MetaData, RequestParameters
```

**Infrastructure**

```
Persistence/
  AppDbContext.cs
  Configurations/       UserConfiguration, RoleConfiguration, ...
  Repositories/         UserRepository, RoleRepository
  Migrations/
  Interceptors/         DomainEventDispatchInterceptor, AuditInterceptor
Identity/               Argon2PasswordHasher, JwtTokenService, RefreshTokenStore
Outbox/                 OutboxMessage, OutboxProcessor (BackgroundService)
```

**Api**

```
Program.cs
Extensions/             ServiceCollection and WebApplication extensions
Controllers/V1/         UsersController, RolesController, AuthController, RootController
Contracts/V1/           RegisterUserRequest, UpdateUserRequest, LoginRequest
Mapping/                RequestMappers (Mapperly)
Errors/                 DomainExceptionHandler, NotFoundExceptionHandler, GlobalExceptionHandler
Filters/                ValidateMediaTypeAttribute
```

### 6.4 Model types

Four distinct groups. There is no separate "database model".

| Group | Lives in | Examples | Crosses a boundary? |
|---|---|---|---|
| HTTP contracts | `Api` | `RegisterUserRequest`, `UserDto`, `PagedList<UserDto>` | Serialized to the client. Versioned. |
| Application messages | `Application` | `RegisterUserCommand`, `GetUsersQuery`, `Result<T>` | Never serialized. Internal only. |
| Domain model | `Domain` | `User`, `Email`, `UserStatus` | Also the persistence model. |
| Infrastructure types | `Infrastructure` | `OutboxMessage`, `RefreshToken`, `UserConfiguration` | Never leave the layer. |

The domain entity is mapped to its table by `IEntityTypeConfiguration<T>` in `Infrastructure`, so the domain classes carry no persistence attributes. Value objects map as owned types into the owner's table.

HTTP contracts are separate from commands on purpose. The contract is public and versioned; when v2 changes the request shape, `RegisterUserRequestV2` changes and the command and domain do not. The cost is one small generated mapper per route.

### 6.5 Write path

```
POST /api/v1/users
  → middleware pipeline
  → UsersController: request → command
  → dispatcher → LoggingBehavior → ValidationBehavior → TransactionBehavior
  → RegisterUserCommandHandler: orchestration only
  → User.Register(...): invariants, raises UserRegisteredEvent
  → IUserRepository.Add + IUnitOfWork.SaveChangesAsync
  → interceptor dispatches domain events, then commits
  → 201 Created + Location header
```

The handler contains no `if` statement that expresses a business rule. If a rule appears there, it belongs in the aggregate.

Domain events dispatch **before** the commit, inside the same transaction. A failure in an event handler rolls back the original write. Events that must cross the service boundary are written to the outbox table in the same transaction and published afterwards by a background worker.

### 6.6 Read path

```
GET /api/v1/users?pageNumber=2&pageSize=20&searchTerm=ana&orderBy=email desc
  → middleware pipeline
  → UsersController: query string → GetUsersQuery
  → dispatcher → LoggingBehavior → ValidationBehavior   (no transaction)
  → GetUsersQueryHandler: DbContext.Users.AsNoTracking()
        .Filter(...).Search(...).Sort(...).Select(→ UserDto)
  → PagedList<UserDto>
  → 200 OK + ETag + X-Pagination header
```

No aggregates, no repositories on this path. `Select` into the DTO translates into SQL, so the database returns only the columns that are needed.

### 6.7 Middleware order

```
Serilog request logging
IExceptionHandler chain → ProblemDetails
HTTPS redirection
Routing
Rate limiter
CORS
Authentication
Authorization
Endpoints: controllers, /health, /openapi, /scalar
```

The order is not stylistic. Rate limiting must sit after routing, because per-endpoint policies need the resolved endpoint. CORS must precede authentication so preflight requests are answered before an auth challenge. Authentication precedes authorization for obvious reasons.

Pipeline behaviors are not middleware. Middleware operates on `HttpContext` and knows nothing about commands. Behaviors operate on commands and know nothing about HTTP. They run inside the endpoint, after the controller has taken the request.

### 6.8 Database schema

```
users              id uuid PK, email citext UNIQUE, password_hash text,
                   name_first text, name_last text, status_id int,
                   created_at timestamptz, updated_at timestamptz
roles              id uuid PK, name text UNIQUE
permissions        id uuid PK, code text UNIQUE
user_roles         user_id FK, role_id FK, assigned_at
role_permissions   role_id FK, permission_id FK
refresh_tokens     id uuid PK, user_id FK, token_hash text,
                   expires_at timestamptz, revoked_at timestamptz, replaced_by uuid
audit_log          id bigserial PK, actor_id uuid, action text,
                   entity text, entity_id uuid, payload jsonb, at timestamptz
outbox_messages    id uuid PK, type text, payload jsonb,
                   occurred_at timestamptz, processed_at timestamptz, attempts int
```

**Aggregate boundaries** are wider than the foreign keys suggest:

- `User` aggregate = `users` + `user_roles`. `UserRole` changes only through `user.AssignRole(roleId)`.
- `Role` aggregate = `roles` + `role_permissions`.
- `RefreshToken` is its own aggregate. It rotates on every refresh, independently of the user; putting it inside `User` would load and lock the whole user object on every token exchange.
- `audit_log` and `outbox_messages` are infrastructure, not domain.

References between aggregates are foreign keys only, never navigation properties: `UserRole` holds a `RoleId`, not a `Role`. This is the single rule that keeps aggregates from collapsing into one graph.

Schema choices worth noting: `citext` for email gives case-insensitive uniqueness without a functional index and without `LOWER()` on every lookup. `status_id` is an `int` because `UserStatus` is an enumeration class, not a C# `enum`. `replaced_by` in `refresh_tokens` records the rotation chain, which is what makes reuse detection possible — if a token that was already exchanged shows up again, the chain is compromised and every token for that user is revoked.

---

## 7. Execution plan

Six milestones, 26 pull requests. Each PR is one branch, one squash-merged commit on `main`, and leaves the build green.

Estimated at roughly 10–12 hours per week: **7 to 8 weeks**.

### M0 — Foundation (3 PRs, ~4 days)

| PR | Branch | Contents |
|---|---|---|
| 1 | `chore/solution-skeleton` | Solution, 4 source projects, 3 test projects, `Directory.Build.props` with `Nullable=enable`, `TreatWarningsAsErrors=true`, `LangVersion=latest`. `.editorconfig`. README stub. |
| 2 | `ci/build-and-test` | GitHub Actions: restore, build, test, coverage. Multi-stage `Dockerfile`. `docker-compose.yml` with a local PostgreSQL for offline work. |
| 3 | `feat/api-foundation` | Serilog with correlation ID, `AddProblemDetails`, `IExceptionHandler` chain, health checks at `/health`, OpenAPI 3.1 + Scalar at `/scalar`, user-secrets wiring. |

**Exit:** `docker compose up` runs, `/health` returns healthy, `/scalar` renders, CI is green.

### M1 — Domain (3 PRs, ~1 week)

| PR | Branch | Contents |
|---|---|---|
| 4 | `feat/domain-seedwork` | `Entity`, `AggregateRoot`, `ValueObject`, `Enumeration`, `IDomainEvent`, `Error`, `Result<T>`, `DomainException`. Full unit tests on equality semantics. |
| 5 | `feat/domain-user-aggregate` | `Email`, `PersonName`, `PasswordHash` value objects with validation in constructors. `UserStatus` enumeration. `User.Register`, `ChangeEmail`, `Lock`, `Unlock`. `UserRegisteredEvent`. |
| 6 | `feat/domain-roles` | `Role`, `Permission`, `RolePermission`, `UserRole`. `user.AssignRole` / `RemoveRole` with the invariant that a user always retains at least one role. |

**Exit:** the domain project compiles with no external package reference, and its test project covers every public method on every aggregate.

### M2 — Persistence (3 PRs, ~1 week)

| PR | Branch | Contents |
|---|---|---|
| 7 | `feat/persistence-dbcontext` | `AppDbContext`, one `IEntityTypeConfiguration` per aggregate, owned types for value objects, snake_case naming convention, `citext` extension, initial migration. |
| 8 | `feat/persistence-repositories` | `UserRepository`, `RoleRepository`, `IUnitOfWork` on the context, `DomainEventDispatchInterceptor`. Integration tests against PostgreSQL via Testcontainers. |
| 9 | `feat/persistence-seed` | Seed roles, permissions and a bootstrap administrator. Migration strategy documented. Connect to the Neon `dev` branch. |

**Exit:** `dotnet ef database update` against Neon succeeds; integration tests pass in CI against a containerised PostgreSQL.

### M3 — Application and write path (4 PRs, ~1.5 weeks)

| PR | Branch | Contents |
|---|---|---|
| 10 | `feat/application-dispatcher` | `ICommand`, `IQuery`, handler interfaces, `Dispatcher`, assembly-scanning DI registration. |
| 11 | `feat/application-behaviors` | `LoggingBehavior`, `ValidationBehavior` (FluentValidation), `TransactionBehavior` wrapped in the EF execution strategy. |
| 12 | `feat/users-write-endpoints` | `RegisterUser`, `UpdateUser`, `LockUser`, `AssignRole` commands with validators, handlers, controller actions, request contracts, Mapperly mappers. POST/PUT/PATCH/DELETE with correct status codes. |
| 13 | `feat/outbox` | `OutboxMessage`, write inside the transaction, `OutboxProcessor` background service with retry and backoff. |

**Exit:** a user can be created, updated, locked and assigned a role, with validation failures returning 422 problem details.

### M4 — Authentication and authorization (4 PRs, ~1.5 weeks)

| PR | Branch | Contents |
|---|---|---|
| 14 | `feat/auth-login` | Argon2id hashing with tuned parameters, JWT issuing, `POST /auth/login`, JWT bearer configuration. |
| 15 | `feat/auth-refresh-rotation` | `POST /auth/refresh` with token rotation, `replaced_by` chain, reuse detection that revokes the whole chain, `POST /auth/logout`. |
| 16 | `feat/authz-permissions` | Permission-based policies, `PermissionAuthorizationHandler`, `[HasPermission("users.write")]`, permissions embedded as claims. |
| 17 | `feat/audit-log` | `ICurrentUser` accessor, audit interceptor, immutable audit entries. |

**Exit:** the full auth flow works end to end and every mutating endpoint is protected by a specific permission, not just `[Authorize]`.

### M5 — Read path and REST surface (5 PRs, ~1.5 weeks)

| PR | Branch | Contents |
|---|---|---|
| 18 | `feat/users-read-endpoints` | Query handlers, projections, `UserDto` / `UserDetailsDto`, GET collection and GET by id. |
| 19 | `feat/collection-querying` | `PagedList`, `MetaData`, `X-Pagination` header, filtering, searching, dynamic sorting with a whitelist of sortable fields. |
| 20 | `feat/http-caching` | ETag generation, `If-None-Match` handling, 304 responses, `Cache-Control` per endpoint. |
| 21 | `feat/api-versioning` | `Asp.Versioning` v10, URL-segment versioning, v1 and v2 grouped separately in OpenAPI. |
| 22 | `feat/hateoas` | Link generation behind `application/vnd.umapi.hateoas+json`, media type validation filter, root document, OPTIONS and HEAD support. |

**Exit:** the collection endpoint supports paging, filtering, searching, sorting and conditional GET, with an opt-in hypermedia representation.

### M6 — Hardening and delivery (4 PRs, ~1 week)

| PR | Branch | Contents |
|---|---|---|
| 23 | `test/functional-suite` | `WebApplicationFactory` + Testcontainers, full-stack tests covering register → login → refresh → protected read. |
| 24 | `feat/hardening` | Per-endpoint rate limiting, security headers, tightened CORS policy, request size limits. |
| 25 | `feat/observability` | OpenTelemetry traces and metrics, database health check, structured log enrichment. |
| 26 | `docs/release-1.0` | README with architecture diagram and quick start, `architecture.md`, ADR index, `.http` request collection, deployed instance. |

**Exit:** `v1.0.0` tagged, README complete, CI green, live demo reachable.

---

## 8. Architecture decision records

One file per decision in `docs/adr/`, in the format: Context → Decision → Consequences. Written when the decision is made, not retroactively.

| # | Decision |
|---|---|
| 0001 | Four-project layering with dependencies pointing inward |
| 0002 | PostgreSQL on Neon, with branch-per-environment |
| 0003 | CQRS with a hand-written dispatcher instead of a mediator library |
| 0004 | The domain model is the persistence model; no separate DB entities |
| 0005 | Value objects mapped as EF Core owned types |
| 0006 | Enumeration classes instead of C# enums for domain states |
| 0007 | Domain events dispatched before commit, inside the same transaction |
| 0008 | Outbox pattern for events that leave the service |
| 0009 | Short-lived JWTs with rotating refresh tokens and reuse detection |
| 0010 | Permission-based authorization policies rather than role checks |
| 0011 | HTTP contracts kept separate from application commands |
| 0012 | Testcontainers instead of the EF in-memory provider |
| 0013 | HATEOAS behind an opt-in media type |
| 0014 | Mapperly for compile-time mapping |
| 0015 | Multi-tenancy and OIDC explicitly out of scope |

ADR 0015 matters as much as the rest. Recording what was deliberately excluded, and why, reads as judgement. A scope that grows without a record reads as drift.

---

## 9. Testing strategy

| Layer | Type | Tooling | What it covers |
|---|---|---|---|
| Domain | Unit | xUnit, FluentAssertions | Every aggregate method, every invariant, every value object. No mocks needed — the domain has no dependencies. |
| Application | Unit | xUnit, NSubstitute | Handler orchestration with faked repositories. Behaviors. Validators. |
| Infrastructure | Integration | Testcontainers + PostgreSQL | Mappings, migrations, queries, the event dispatch interceptor, concurrency. |
| API | Functional | `WebApplicationFactory` + Testcontainers | Real HTTP over the full stack including middleware, auth and model binding. |

Rules:

- No in-memory database provider. It does not enforce constraints, does not translate the same SQL, and passes tests that fail against PostgreSQL.
- One assertion concept per test. Test names read as sentences: `Register_ReturnsFailure_WhenEmailAlreadyExists`.
- Coverage is a diagnostic, not a target. A method with a conditional and no test is a gap worth fixing; a tested auto-property is noise.
- Every bug fix starts with a failing test that reproduces it.

---

## 10. CI pipeline

`.github/workflows/ci.yml`, running on push to `main` and on every PR:

1. Checkout, set up .NET 10
2. `dotnet restore`
3. `dotnet build --no-restore -warnaserror`
4. `dotnet test --no-build --collect:"XPlat Code Coverage"`
5. Upload the coverage report as an artifact
6. Build the Docker image (do not push on PRs)

Integration tests run in CI against a Testcontainers PostgreSQL, so no database secret is needed. Docker is available on `ubuntu-latest` runners by default.

---

## 11. Definition of done

A PR is ready to merge when all of these hold:

- [ ] Build passes with warnings as errors
- [ ] New behavior is covered by a test at the appropriate level
- [ ] No business logic sits outside the domain
- [ ] Public endpoints appear correctly in the OpenAPI document
- [ ] Errors return RFC 9457 problem details with the right status code
- [ ] No secret, connection string or key is in the diff
- [ ] The PR description explains why, not only what
- [ ] The ADR is written if the PR implements a decision

---

## 12. README

The README is what gets read. Most people who open the repository will read it and nothing else, so it is built with the same care as the code.

Structure:

1. One-paragraph description of what the API does
2. Architecture diagram (the layer/dependency diagram, exported as SVG)
3. Feature list, grouped: domain modelling, security, HTTP surface, operations
4. Quick start — clone, one `docker compose up`, one URL, working in under two minutes
5. Screenshot of the Scalar UI
6. Link to `architecture.md` and the ADR index
7. Test and coverage badges
8. Tech stack table

The quick start is the part to get right. If someone has to configure a database before they can see anything, they close the tab. `docker compose up` should bring up PostgreSQL, apply migrations, seed data and start the API.

---

## 13. What else — things worth deciding now

**Cadence beats intensity.** A steady commit history over eight weeks reads as real work. Nine hundred lines committed at 3 a.m. on a single Sunday reads as generated. Commit on the days you work, and do not backdate anything.

**Deploy it.** An unreachable project is a screenshot. Neon free tier plus a container on Fly.io or Render costs nothing and turns the README from a description into a demo. Seed a read-only account and put the credentials in the README so anyone can log in and click through Scalar.

**Ship v1.0 at M4 if time pressure appears.** M0 through M4 is already a complete, defensible API. M5 and M6 are what raise it from good to distinctive. If the job search accelerates, tag `v1.0.0` after M4 and continue in the open rather than leaving an unfinished `main`.

**Keep the scope closed.** The strongest signal in a portfolio project is a finished one. Ideas for multi-tenancy, OIDC, webhooks and admin UIs go into GitHub issues labelled `future`, not into `main`. Issues that are open and reasoned about look like planning; half-built features look like abandonment.

**Write the CV bullets from the ADRs.** When this is done, the two or three lines on the CV come straight from the decisions that were recorded — modelled X, chose Y over Z for reason W. That is a much stronger sentence than a list of technologies, and it survives the follow-up question in an interview.

**Be accurate about what it is.** It is a personal project built to a production standard. Not "used in production", not "serving N users". The work stands on its own; overstating it is the only thing that could undermine it.

---

## 14. What actually happened

The plan above is the one the project was built to. Six milestones, twenty-six
pull requests, and `v1.0.0` tagged at the end of M6. These are the places where
the finished repository differs from the plan, each recorded in the pull request
that made the change.

| Where | Plan | What shipped |
|---|---|---|
| §6.3, M5 | Query handlers in `Application`, reading `DbContext` | Handlers in `Infrastructure`; queries, read models and `PagedList<T>` stay in `Application`, which keeps its "no EF Core" invariant (ADR 0016) |
| §6.7 | Rate limiter before authentication | Authentication first: the read and write policies partition by user id, which does not exist until the token has been read |
| §7 M5 | Per-version OpenAPI documents from two explicit `AddOpenApi` calls | `Asp.Versioning.OpenApi` 10.2.3, stable since 30 July 2026. Analyzers AV0029/AV0030 refuse the two-document setup, and without `WithDocumentPerVersion()` the per-version documents are generated but never served |
| §7 M5 | An unsupported version returns 400 | 404. With the version in the URL segment, `Asp.Versioning` treats an unknown version as an address that does not exist |
| §7 M6 | A malformed body returns 422 | 400. A body that is not JSON never becomes a command, so no validator can name a field; 422 is kept for a well-formed body with bad values |
| §7 M5 | Data shaping was never planned, and is explicitly declined | ADR 0017 records why: a shaped response no longer matches the OpenAPI schema |
| §8 | Fifteen ADRs | Eighteen. ADR 0016 (query handlers) and 0017 (no data shaping) came out of M5; 0018 (JSON console logs in production) came out of a CodeQL finding after `v1.0.0` |
| §13 | Deploy to Fly.io or Render | Render, Frankfurt, free tier, with Neon as the database |
| M5 | `RolesController` appears in no milestone | Added in M5 PR18: a client needs a role id to assign one, and `roles.read` protected nothing without it |
| §6.2 | An architecture test asserting the layer rules | Added after `v1.0.0`, and in two projects rather than one: the controller rule needs a project that references both `Api` and `Infrastructure`. Until then the rules were greps inside a helper script that is never committed, so nothing enforced them. Test baseline 266 → 269 |
| §3, §12 | Local setup instructions and the per-milestone build guides in `docs/` | Removed. They described how this repository was produced and on which machine, not what it is; one of them also duplicated SQL that its migration already carries |
| §10 | One workflow: build, test, coverage, image | Two. A CodeQL workflow analyses C# on every pull request and weekly, with `build-mode: manual` because autobuild infers a build that central package management makes hard to infer. Dependabot watches NuGet and the actions themselves — the actions ecosystem earns it: three of the four actions in `ci.yml` were still on the Node 20 runtime that GitHub removes on 16 September 2026 |
| §13 | `usermanagementapi.onrender.com` | `usermanagementapi-j1if.onrender.com`. The short name is taken globally, and Render appends a suffix; the README published the name this project asked for rather than the one it got |
| §6.7 | Nothing about the root path | `GET /` and `HEAD /` redirect to the reference, or to the root document when the documentation is switched off. The first deploy answered a bare 404 there |
| §2 | `FluentAssertions` as a test dependency | Pinned to 7.2.0 and its major updates ignored. Version 8 replaced Apache 2.0 with the Xceed Community License — free for non-commercial use, paid for commercial use. Section 2 already declines a dependency over licensing constraints, and 7.x remains Apache 2.0 with fixes, so the pin is the consistent choice rather than a new one |
| §11 | Errors return problem details with the right status code | They did not for one case. A body over the Kestrel limit was refused with 413 by the server and then answered 500, because `BadHttpRequestException` reached `GlobalExceptionHandler`. A handler ahead of it now uses the status the exception carries and logs a warning instead of an error. Found by hand, because `TestServer` is not Kestrel and cannot raise it |
