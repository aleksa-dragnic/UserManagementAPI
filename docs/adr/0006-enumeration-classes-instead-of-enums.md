# 0006 — Enumeration classes instead of C# enums for domain states

## Context

Domain states such as user status need a closed set of named values. A C# `enum`
is the default choice, but it is an `int` with a label attached: any integer can
be cast into it, including one that names no member, and the compiler accepts
it. It also cannot carry behaviour, so logic that belongs to a state ends up in
`switch` statements spread across the codebase.

EF Core maps an `enum` to an `int` column happily, which means an out-of-range
value can be written to and read back from the database without anything
objecting.

## Decision

Domain states are modelled as classes deriving from `Enumeration`. Instances are
declared as `public static readonly` fields on the derived type. `GetAll<T>()`
reflects over those fields; `FromId` and `FromName` throw when no instance
matches, so an unknown value fails at the boundary rather than propagating.

Equality is by concrete type and id. `IComparable` is implemented so a set of
states has a natural order for display.

## Consequences

An invalid state cannot be constructed — the constructor is private on every
derived type and the only instances are the declared ones. A state can carry
behaviour when it needs to.

The cost is a reflective lookup in `GetAll<T>()` and one extra line of EF Core
configuration per property, converting the instance to its `Id` for storage
(ADR 0004 covers the mapping side).

`FromId` throwing rather than returning a `Result` is deliberate: an unknown id
coming out of the database is corruption, not a user error, and there is no
caller that could sensibly handle it.