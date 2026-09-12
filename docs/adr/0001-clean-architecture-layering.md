# 0001 — Four projects, with dependencies pointing inward

## Context

Every non-trivial service has the same four concerns: the rules of the
business, the use cases that orchestrate them, the technology that stores and
transports data, and the HTTP surface. How they are arranged decides what can
be changed without touching everything else.

The default arrangement — one web project with Controllers, Services and Data
folders — has the dependency arrow pointing outward: the rules end up depending
on Entity Framework, because that is where the entities live. Changing the
database, or testing a rule, means starting the whole stack.

This ADR is written late, in M6. The decision was made and followed from the
first commit; recording it at the end is the exception that the rest of the
ADRs avoid, and it is recorded as such rather than backdated.

## Decision

Four projects:

    Api ──────────► Application ──────────► Domain
     │                                        ▲
     └╌╌╌╌╌╌► Infrastructure ─────────────────┘
            (composition root only)

`Domain` references nothing but the base class library — no EF Core package, no
ASP.NET package. `Application` references `Domain` and defines the interfaces
it needs (`IUserRepository`, `IUnitOfWork`, `IPasswordHasher`, `ITokenService`)
for someone else to implement. `Infrastructure` references both and implements
them. `Api` references `Application`, and `Infrastructure` only in `Program.cs`
and the extension methods it calls, to wire the container.

The rule is enforced rather than documented: the apply script for every PR
greps the Domain project for a `PackageReference`, greps Application for an EF
Core or ASP.NET using, and greps the controllers for an Infrastructure type.
A violation fails the run.

## Consequences

The domain is testable with no mocks and no container, because it has no
dependencies to fake — its test project is the fastest in the solution.
Swapping PostgreSQL, the hasher or the token format touches Infrastructure
only; swapping the transport touches Api only.

Two prices. There are more projects than a small service needs, and a feature
touches several of them at once — a new use case is a command in Application, a
handler, a contract and an action in Api. And an interface sometimes exists for
one implementation, which is a cost paid for the direction of the arrow rather
than for polymorphism.

Query handlers are the one deliberate exception, recorded separately in ADR
0016: they live in Infrastructure, because the read path is meant to reach the
database directly and `Application` must stay free of EF Core.
