# Architecture decision records

One file per decision, in the format Context → Decision → Consequences, written
in the pull request that implements it.

| # | Decision | Milestone |
|---|---|---|
| [0001](0001-clean-architecture-layering.md) | Four projects, with dependencies pointing inward | M6 (recorded late) |
| [0002](0002-postgresql-on-neon-branch-per-environment.md) | PostgreSQL on Neon, branch per environment | M2 |
| [0003](0003-cqrs-with-a-hand-written-dispatcher.md) | CQRS with a hand-written dispatcher instead of a mediator library | M3 |
| [0004](0004-domain-model-is-the-persistence-model.md) | The domain model is the persistence model; no separate database entities | M2 |
| [0005](0005-value-objects-as-owned-types.md) | Value objects mapped as EF Core owned types | M2 |
| [0006](0006-enumeration-classes-instead-of-enums.md) | Enumeration classes instead of C# enums for domain states | M2 |
| [0007](0007-domain-events-dispatched-before-commit.md) | Domain events dispatched before commit, inside the same transaction | M2 |
| [0008](0008-outbox-pattern-for-events-that-leave-the-service.md) | Outbox pattern for events that leave the service | M3 |
| [0009](0009-short-lived-jwts-with-rotating-refresh-tokens.md) | Short-lived JWTs with rotating refresh tokens and reuse detection | M4 |
| [0010](0010-permission-based-authorization-policies.md) | Permission-based authorization policies rather than role checks | M4 |
| [0011](0011-http-contracts-separate-from-commands.md) | HTTP contracts kept separate from application commands | M3 |
| [0012](0012-testcontainers-instead-of-in-memory-provider.md) | Testcontainers instead of the EF in-memory provider | M2 |
| [0013](0013-hateoas-via-vendor-media-type.md) | HATEOAS on request, through a vendor media type | M5 |
| [0014](0014-mapperly-for-compile-time-mapping.md) | Mapperly for compile-time mapping | M3 |
| [0015](0015-multi-tenancy-and-oidc-out-of-scope.md) | Multi-tenancy and OIDC explicitly out of scope | M4 |
| [0016](0016-query-handlers-in-infrastructure.md) | Query handlers live in Infrastructure | M5 |
| [0017](0017-no-data-shaping.md) | No data shaping on collection endpoints | M5 |
