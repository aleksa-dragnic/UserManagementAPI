# 0005 — Value objects mapped as EF Core owned types

## Context

`Email`, `PersonName` and `PasswordHash` are value objects: they have no
identity and are meaningless apart from the user that holds them. Mapping each
to its own table would give them a primary key and a lifecycle they should not
have, and would turn loading a user into a join.

## Decision

Value objects map as owned types into the owner's table. `Email` becomes the
`email` column, `PersonName` becomes `name_first` and `name_last`,
`PasswordHash` becomes `password_hash`. Each navigation is marked required, so
a user without a name is rejected by the schema and not only by the aggregate.

`email` is a `citext` column with a unique index. Case-insensitive uniqueness
comes from the column type rather than a functional index, so no lookup needs
`LOWER()` and no query silently misses the index by forgetting it.

## Consequences

A user is one row and one read. The value object round-trips as itself, so code
that received an `Email` from the database has the same guarantees as code that
built one through `Email.Create`.

Two constraints follow. An owned type cannot be compared as a whole in a LINQ
query — `user.Email == email` does not translate, so repositories compare
`user.Email.Value == email.Value`. And a query filtering on a value object
property is filtering on a column of the owner's table, which is what makes the
projections in M5 possible without a join.

`citext` requires the extension, enabled in `OnModelCreating` via
`HasPostgresExtension` so it is created by the migration rather than by hand on
each environment.