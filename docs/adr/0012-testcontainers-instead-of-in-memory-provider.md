# 0012 — Testcontainers instead of the EF Core in-memory provider

## Context

Integration tests need a database. The in-memory provider is the convenient
option: no Docker, instant startup, nothing to clean up.

It is also not a database. It enforces no unique index, no foreign key and no
check constraint. It does not translate LINQ to SQL, so a query it happily
answers in memory may not translate against PostgreSQL at all — and one that
does may translate differently. It has no `citext`, no `jsonb`, no transaction
semantics worth the name.

The failure mode is the expensive one: the test suite is green and the
behaviour it claims to cover is broken in production.

## Decision

Integration tests run against a real PostgreSQL 17 started by Testcontainers.
One container is shared by the whole test run through an xUnit collection
fixture, migrated once on start. Tests truncate between runs rather than
recreating the schema.

The in-memory provider is not used anywhere, for anything.

## Consequences

The tests that matter actually test something: the unique index on `citext`
email rejects a duplicate that differs only in case, the enumeration conversion
round-trips through an int column, and a throwing event handler rolls back the
write — none of which the in-memory provider could have caught.

The costs are real but small. Docker must be running locally; the first run
pulls the image and is slow once. CI needs Docker, which `ubuntu-latest`
provides by default, and needs no database secret at all, because the container
supplies its own.

Shared state is the trade-off of a shared container. Tests reset the rows in
their fixture rather than assuming an empty database, and no test may depend on
another test's data.