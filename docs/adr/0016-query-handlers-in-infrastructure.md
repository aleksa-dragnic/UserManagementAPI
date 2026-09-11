# 0016 — Query handlers live in Infrastructure

## Context

The read side is meant to skip the domain entirely (ADR 0003, M5): a query
handler reads the `DbContext` with `AsNoTracking`, projects straight into a
DTO, and lets the database return only the columns the DTO needs.

The layering says something that pulls the other way. Application references
Domain and nothing that knows about a database; "Application has no EF Core
using" is one of the invariants checked on every PR. A handler that calls
`ToListAsync` on a `DbSet` needs EF Core, so it cannot live in Application
without breaking that rule.

The two common ways out both give something up. An `IApplicationDbContext`
interface in Application that exposes `DbSet<T>` puts EF Core types in
Application's public surface — the rule is broken, only more politely. A
repository per query (`IUserQueries.GetPagedAsync(...)`) keeps Application
clean, but the handler becomes a one-line pass-through and the actual query
moves behind an interface that exists for one caller.

## Decision

The query contract lives in Application: the query record, the read models
(`UserDto`, `RoleDto`, `PagedList<T>`), the validators, and the query
extensions that are plain LINQ over `IQueryable<User>`. The handler lives in
Infrastructure, in `Queries/`, and reads `AppDbContext` directly.

`AddInfrastructure` registers every `IQueryHandler<,>` in its own assembly by
scanning, the same way `AddApplication` registers command handlers. The
dispatcher does not care which assembly a handler comes from; the validation
and logging behaviors wrap a query exactly as they did before.

`IPermissionLookup` (M4) was the first instance of this shape — an interface in
Application, one join in Infrastructure — and is left as it is.

## Consequences

Application stays free of EF Core, and the handler does what M5 asks of it:
`AsNoTracking`, `Select` into the DTO, the projection in the SQL. A test reads
the generated SQL and fails if the password hash is ever selected.

A reader looking for "the handler for GetUsersQuery" finds it in Infrastructure,
not beside the query. The folder names match (`Application/Users/Queries/GetUsers`,
`Infrastructure/Queries/Users`) so the jump is short.

Query handlers are tested against PostgreSQL through the API rather than with a
faked repository. For code whose whole job is to produce the right SQL, that is
the test that means something.
